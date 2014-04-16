using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KSoft
{
    /// <summary>
    /// Получает сообщения из сети.
    /// </summary>
    public class NetReceiver
        : ReceiverBase
    {
        Socket m_socket;
        int m_connectionCount;

        ProtocolType m_protocolType;
        public ProtocolType ProtocolType
        {
            get { return m_protocolType; }
            set
            {
                if (value == ProtocolType.Tcp || value == ProtocolType.Udp)
                    m_protocolType = value;
                else
                    throw new ArgumentException("ProtocolType must be Tcp or Udp");
            }
        }

        IPAddress m_address = IPAddress.Any;
        public IPAddress Address
        {
            get { return m_address; }
            set { m_address = value; }
        }

        int m_port;
        public int Port
        {
            get { return m_port; }
            set { m_port = value; }
        }

        CancellationTokenSource m_stopTokenSource;
        CancellationToken m_stopToken;
        protected CancellationToken StopToken
        {
            get
            {
                lock(this)
                {
                    if (m_stopTokenSource != null)
                        return m_stopToken;
                    else
                        return new CancellationToken(true);
                }
            }
        }

        protected override void StartInternal()
        {
            m_stopTokenSource = new CancellationTokenSource();
            m_stopToken = m_stopTokenSource.Token;
            InitSocket();
            WaitForConnection();
        }

        void InitSocket()
        {
            if (m_socket == null)
            {
                Debug.Assert(m_protocolType == ProtocolType.Tcp || m_protocolType == ProtocolType.Udp);

                m_socket = new Socket(AddressFamily.InterNetwork, m_protocolType == ProtocolType.Udp ? SocketType.Dgram : SocketType.Stream, m_protocolType);
                EndPoint ep = new IPEndPoint(m_address, m_port);
                m_connectionCount = 0;
                m_socket.Bind(ep);
                m_socket.Listen(1024);
                TraceHelper.WriteEvent(this, "Waiting connection on {0}", m_socket.LocalEndPoint);
            }
        }

        void WaitForConnection()
        {
            var e = new SocketAsyncEventArgs();
            e.Completed += SocketAcceptCompleted;
            if (!m_socket.AcceptAsync(e))
                AcceptConnection(e);
        }

        void SocketAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            AcceptConnection(e);
        }

        /// <summary>
        /// Максимальное время неответа объекта = 3 мин.
        /// </summary>
        const int MaxTestTimeout = 180000;

        void AcceptConnection(SocketAsyncEventArgs e)
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid(); // new activity
            if (!StopToken.IsCancellationRequested)
                WaitForConnection();
            EndPoint endpoint = null;
            if (e.SocketError == SocketError.Success)
            {
                int connCount = Interlocked.Increment(ref m_connectionCount);
                endpoint = e.AcceptSocket.RemoteEndPoint;
                TraceHelper.WriteEvent(this, "Accepted connection from {0} ({1} connections total)", endpoint, connCount);
                //OnMessageReceived(new ServiceMessage() {Type = MessageType.Connect});
            }
            else
            {
                EndPoint ep = null;
                Socket sock = e.AcceptSocket;
                if (sock != null)
                    ep = sock.RemoteEndPoint;
                TraceHelper.WriteError(this, "Error accepting connection from {0}: {1}", ep, e.SocketError);
                return;
            }

            var msgReader = GetMessageReader();
            DataStream stream = null;
            try
            {
                stream = new NetDataStream(e.AcceptSocket, false);
                e.AcceptSocket.ReceiveTimeout = MaxTestTimeout;
                e.AcceptSocket.SendTimeout = 10000; // 10 сек.
                msgReader.Stream = stream;
                bool end = false;
                try
                {
                    msgReader.Initialize();
                }
                catch (Exception ex)
                {
                    OnErrorOccured(new ReceiverException("Не удалось инициализировать подключение", ex) { SourceAddress = e.AcceptSocket.RemoteEndPoint });
                    end = true;
                }

                while (!StopToken.IsCancellationRequested && !end)
                {
                    try
                    {
                        stream.WaitForDataBecomeAvailable(StopToken, timeout: MaxTestTimeout);
                        if (StopToken.IsCancellationRequested || !stream.DataAvailable)
                            end = true;
                        else
                        {
                            IMessage msg = msgReader.ReadMessage(StopToken);
                            if (msg != null)
                            {
                                // Пропишем в Source имя ресивера и, по возможности, адрес (из потока данных)
                                msg.Source = SourceName;
                                string addr = msgReader.Stream.ToString();
                                if (!String.IsNullOrEmpty(addr))
                                    msg.Source += ", " + addr;

                                if (msg is ObjectMessage)
                                    (msg as ObjectMessage).Pult = Pult;
                                OnMessageReceived(msg);
                                msgReader.WriteAnswer(msg, true);
                            }
                            if (msgReader.Stream.EndOfStream)
                                end = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is SocketException)
                            end = true;
                        OnErrorOccured(new ReceiverException(ex.Message, ex) { SourceAddress = e.AcceptSocket.RemoteEndPoint });
                    }
                }
            }
            finally
            {
                ReleaseMessageReader(msgReader);
                var socket = e.AcceptSocket;
                if (socket != null)
                {
                    if (socket.Connected)
                    {
                        try
                        {
                            socket.Shutdown(SocketShutdown.Both);
                            socket.Disconnect(false);
                        }
                        catch
                        { }
                    }
                    socket.Close();
                }
                if (stream != null)
                    stream.Close();
                TraceHelper.WriteEvent(this, "Closed connection from {0} ({1} connections total)", endpoint, Interlocked.Decrement(ref m_connectionCount));
            }
            //if (!StopToken.IsCancellationRequested)
            //    WaitForConnection();
        }

        protected override void StopInternal()
        {
            lock(this)
            {
                m_stopTokenSource.Cancel();
                if (m_socket != null)
                {
                    m_socket.Close();
                    m_socket = null;
                }
            }
        }
    }
}
