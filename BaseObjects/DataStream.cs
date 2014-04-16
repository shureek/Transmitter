using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KSoft
{
    /// <summary>
    /// Поток данных.
    /// </summary>
    /// <remarks>
    /// Это своего рода надстройка над другими потоками. Содержит информацию об источнике потока (порт, удаленный адрес и т. п.).
    /// Экземпляры этого класса не потокобезопасны.
    /// Идея такая. Нужен поток (Stream), поддерживающий асинхронные операции чтения (ReadAsync).
    /// При чем, одновременно может быть несколько сотен потоков с вызванным ReadAsync.
    /// </remarks>
    public abstract class DataStream
        : Stream
    {
        Stream m_stream;
        readonly object m_dataObject;

        Encoding m_encoding;
        /// <summary>
        /// Кодировка текста (get, set).
        /// </summary>
        public Encoding Encoding
        {
            get { return m_encoding; }
            set { m_encoding = value; }
        }

        EventHandler<DataAvailableEventArgs> m_dataBecomeAvailableHandler;
        /// <summary>
        /// Событие, наступающее при появлении данных в потоке.
        /// </summary>
        public event EventHandler<DataAvailableEventArgs> DataBecomeAvailable
        {
            add
            {
                lock(this)
                {
                    bool handlerWasEmpty = (m_dataBecomeAvailableHandler == null);
                    m_dataBecomeAvailableHandler += value;
                    bool handlerIsSet = (m_dataBecomeAvailableHandler != null);
                    if (handlerWasEmpty && handlerIsSet)
                        OnDataBecomeAvailableHandlerSet();
                }
            }
            remove
            {
                lock(this)
                {
                    bool handlerWasSet = (m_dataBecomeAvailableHandler != null);
                    m_dataBecomeAvailableHandler -= value;
                    bool handlerIsEmpty = (m_dataBecomeAvailableHandler == null);
                    if (handlerWasSet && handlerIsEmpty)
                        OnDataBecomeAvailableHandlerRemoved();
                }
            }
        }

        protected virtual void OnDataBecomeAvailableHandlerSet() { }
        protected virtual void OnDataBecomeAvailableHandlerRemoved() { }

        #region [ Buffer factory ]

        static readonly ConcurrentDictionary<int, ArrayFactory<byte>> s_bufferFactories =
            new ConcurrentDictionary<int, ArrayFactory<byte>>(1, 8);

        static readonly object s_bufferLock = new Object();

        static ArrayFactory<byte> GetBufferFactory(int length)
        {
            ArrayFactory<byte> factory;
            lock(s_bufferLock)
            {
                if (!s_bufferFactories.TryGetValue(length, out factory))
                {
                    factory = new ArrayFactory<byte>(true) {ArraySize = length};
                    s_bufferFactories[length] = factory;
                }
            }
            return factory;
        }

        public void PrepareBuffer()
        {
            System.Diagnostics.Debug.Assert(m_buffer == null, "Previous buffer was not released");
            m_buffer = GetBufferFactory(m_bufferSize).Get();
            m_count = 0;
        }

        public void ReleaseBuffer()
        {
            GetBufferFactory(m_buffer.Length).Release(m_buffer);
            m_buffer = null;
        }

        readonly int m_bufferSize;
        byte[] m_buffer;
        int m_count;
        int m_offset;

        #endregion

        #region [ NewLine members ]

        byte[] m_newline;
        public void SetNewLine(byte[] newline)
        {
            m_newline = newline;
        }
        public void SetNewLine(string newline)
        {
            m_newline = Encoding.GetBytes(newline);
        }

        #endregion

        /// <summary>
        /// Базовый поток.
        /// </summary>
        public Stream BaseStream
        {
            get { return m_stream; }
            protected set { m_stream = value; }
        }

        public bool EndOfStream
        {
            get { return Position == Length; }
        }

        string m_name;
        /// <summary>
        /// Имя потока данных (get, set).
        /// </summary>
        public string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        /// <summary>
        /// Объект данных (порт, сокет, и т. п.) (get).
        /// </summary>
        public object DataObject
        {
            get { return m_dataObject; }
        }

        protected DataStream(object dataObject = null, int bufferSize = 256)
        {
            m_dataObject = dataObject;
            m_bufferSize = bufferSize;
        }

        #region [ Write methods ]

        public void Write(byte[] bytes)
        {
            Buffer.BlockCopy(bytes, 0, m_buffer, m_count, bytes.Length);
            m_count += bytes.Length;
        }

        public void Write(IList<byte> bytes)
        {
            // Пишем во временный буфер
            var buffers = bytes.ToBufferList();
            foreach (var buffer in buffers)
            {
                Buffer.BlockCopy(buffer.Array, buffer.Offset, m_buffer, m_count, buffer.Count);
                m_count += buffer.Count;
            }
        }

        public void Write(string str)
        {
            int length = Encoding.GetBytes(str, 0, str.Length, m_buffer, m_count);
            m_count += length;
        }

        #endregion

        public bool ReadTo(IList<byte> ending, out IList<byte> result)
        {
            if (m_buffer == null)
                PrepareBuffer();

            int? eom = null;
            // Если в буфере был неразобранный остаток, то перенесем его в начало буфера
            if (m_offset > 0 && m_count > 0)
                Buffer.BlockCopy(m_buffer, m_offset, m_buffer, 0, m_count);
            m_offset = 0;

            while (true)
            {
                if (m_count > 0)
                {
                    eom = m_buffer.GetSegment(0, m_count).FirstOccurenceOf(ending);
                    if (eom.HasValue)
                        break;
                }

                if (m_count == m_buffer.Length)
                {
                    // Буфер заполнился, а признак конца не найден
                    result = m_buffer;
                    m_count = 0;
                    return false;
                }

                TraceHelper.WriteEvent(this, "Reading {1} bytes from {0}", m_stream, m_buffer.Length - m_count);
                //Trace.WriteLine(String.Format("Read from stream (offset {0}, count {1})", 0, m_buffer.Length - m_count));
                int bytesRead = m_stream.Read(m_buffer, m_count, m_buffer.Length - m_count);
                if (bytesRead == 0)
                {
                    // Конец потока
                    if (m_count > 0)
                    {
                        result = m_buffer.GetSegment(0, m_count);
                        m_count = 0;
                    }
                    else
                        result = null;
                    return false;
                }
                TraceHelper.WriteEvent(this, "Read {2} bytes from {0}: {1}", m_stream, m_buffer.GetSegment(m_count, bytesRead).ToHexString(), bytesRead);
                //Trace.WriteLine(String.Format("{0} bytes read, buffer: {1}", bytesRead, m_buffer.ToHexString()));
                m_count += bytesRead;
            }

            int msgLength = eom.Value + ending.Count;
            result = m_buffer.GetSegment(0, msgLength);
            m_offset = msgLength;
            m_count -= msgLength;
            return true;
        }

        public IList<byte> ReadTo(IList<byte> ending)
        {
            IList<byte> result;
            ReadTo(ending, out result);
            return result;
        }

        public string ReadLine()
        {
            IList<byte> bytes = ReadTo(m_newline);
            if (bytes != null && bytes.Count > 0)
                // Возьмем строку без признака окончания строки
                return Encoding.GetString(bytes, 0, bytes.Count - m_newline.Length);
            else
                return null;
        }

        /*public IList<string> ReadLines()
        {
            List<string> strings = new List<string>();
            while (true)
            {
                string str = ReadLine();
                if (str == null)
                    break;
                strings.Add(str);
            }
            return strings;
        }*/
        
        protected void OnDataBecomeAvailable(DataAvailableEventArgs e)
        {
            var handler = m_dataBecomeAvailableHandler;
            if (handler != null)
                handler(this, e);
        }

        public bool WaitForDataBecomeAvailable(CancellationToken cancellationToken, int timeout = -1, bool releaseBuffer = true)
        {
            Stopwatch stopwatch = null;
            if (timeout >= 0)
                stopwatch = Stopwatch.StartNew();
            if (releaseBuffer && !DataAvailable)
            {
                // Если данных нет, то освободим буфер на время ожидания данных
                if (m_buffer != null)
                    ReleaseBuffer();
            }

            TraceHelper.WriteEvent(this, "Ожидание данных");
            //TODO: Переделать алгоритм ожидания
            while (!DataAvailable && !cancellationToken.IsCancellationRequested)
            {
                if (timeout >= 0 && stopwatch.ElapsedMilliseconds >= timeout)
                    return false;
                Thread.Sleep(1000);
            }

            if (m_buffer == null)
                PrepareBuffer();
            TraceHelper.WriteEvent(this, "Данные пришли");
            return true;
        }

        /// <summary>
        /// Возвращает представление потока в формате Name (DataObject).
        /// </summary>
        /// <returns>Представление потока.</returns>
        public override string ToString()
        {
            string objName = ObjectName;

            if (!String.IsNullOrEmpty(m_name))
            {
                if (!String.IsNullOrEmpty(objName))
                    return String.Format("{0} ({1})", m_name, objName);
                else
                    return m_name;
            }
            else
            {
                if (!String.IsNullOrEmpty(objName))
                    return objName;
                else
                    return base.ToString();
            }
        }

        protected abstract string ObjectName { get; }

        public abstract bool DataAvailable { get; }

        #region [ Base stream members ]

        public override bool CanRead
        {
            get { return m_stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return m_stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return m_stream.CanWrite; }
        }

        public override void Flush()
        {
            if (m_count > 0)
            {
                m_stream.Write(m_buffer, 0, m_count);
                m_count = 0;
            }
            m_stream.Flush();
        }

        public override long Length
        {
            get { return m_stream.Length; }
        }

        public override long Position
        {
            get { return m_stream.Position; }
            set { m_stream.Position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return m_stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return m_stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            m_stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            m_stream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            if (m_buffer != null)
                ReleaseBuffer();
            m_stream.Close();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return m_stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback,
                                                object state)
        {
            return m_stream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override bool CanTimeout
        {
            get
            {
                return m_stream.CanTimeout;
            }
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return m_stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            m_stream.EndWrite(asyncResult);
        }

        public override int ReadByte()
        {
            return m_stream.ReadByte();
        }

        public override int ReadTimeout
        {
            get { return m_stream.ReadTimeout; }
            set { m_stream.ReadTimeout = value; }
        }

        public override void WriteByte(byte value)
        {
            m_stream.WriteByte(value);
        }

        public override int WriteTimeout
        {
            get { return m_stream.WriteTimeout; }
            set { m_stream.WriteTimeout = value; }
        }

        #endregion
    }

    public class SerialDataStream
        : DataStream
    {
        public SerialDataStream(SerialPort port)
            : base(port)
        {
            BaseStream = port.BaseStream;
        }

        void PortOnDataReceived(object sender, SerialDataReceivedEventArgs serialDataReceivedEventArgs)
        {
            OnDataBecomeAvailable(new DataAvailableEventArgs());
        }

        protected override void OnDataBecomeAvailableHandlerSet()
        {
            base.OnDataBecomeAvailableHandlerSet();
            DataObject.DataReceived += PortOnDataReceived;
        }

        protected override void OnDataBecomeAvailableHandlerRemoved()
        {
            base.OnDataBecomeAvailableHandlerRemoved();
            DataObject.DataReceived -= PortOnDataReceived;
        }

        public new SerialPort DataObject
        {
            get { return (SerialPort)base.DataObject; }
        }

        protected override string ObjectName
        {
            get { return DataObject.PortName; }
        }

        public override bool DataAvailable
        {
            get { throw new NotImplementedException(); }
        }
    }

    public class NetDataStream
        : DataStream
    {
        public NetDataStream(Socket socket, bool ownsSocket = false)
            : base(socket)
        {
            base.BaseStream = new NetworkStream(socket, ownsSocket);
        }

        public new Socket DataObject
        {
            get { return (Socket)base.DataObject; }
        }

        public new NetworkStream BaseStream
        {
            get { return (NetworkStream)base.BaseStream; }
        }

        protected override string ObjectName
        {
            get { return String.Format("{0}, {1}", DataObject.RemoteEndPoint, DataObject.ProtocolType); }
        }

        protected override void OnDataBecomeAvailableHandlerSet()
        {
            base.OnDataBecomeAvailableHandlerSet();
            StartWaitingData();
        }

        protected override void OnDataBecomeAvailableHandlerRemoved()
        {
            base.OnDataBecomeAvailableHandlerRemoved();
            StopWaitingData();
        }

        System.Threading.Timer m_dataWaitTimer;

        void StartWaitingData()
        {
            if (BaseStream.DataAvailable)
                OnDataBecomeAvailable(new DataAvailableEventArgs());
            else
            {
                // Устанавливаем таймер на срабатывание каждую секунду
                lock(this)
                {
                    if (m_dataWaitTimer != null)
                        m_dataWaitTimer.Change(1000, 1000);
                    else
                        m_dataWaitTimer = new Timer(WaitTimerCallback, null, 1000, 1000);
                }
                DataObject.Poll(10, SelectMode.SelectRead);
            }
        }

        void WaitTimerCallback(object state)
        {
            if (BaseStream.DataAvailable)
            {
                StopWaitingData();
                OnDataBecomeAvailable(new DataAvailableEventArgs());
            }
        }

        void StopWaitingData()
        {
            m_dataWaitTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public override bool DataAvailable
        {
            get { return BaseStream.DataAvailable; }
        }
    }

    public class DataAvailableEventArgs
        : EventArgs
    {
        
    }
}
