using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace KSoft
{
// ReSharper disable InconsistentNaming
    public class COMReceiver
// ReSharper restore InconsistentNaming
        : AsyncReceiver
    {
        SerialPort m_port;

        #region [ Port properties ]

        string m_portName = "COM1";
        public string PortName
        {
            get { return m_portName; }
            set { m_portName = value; }
        }

        int m_dataBits = 8;
        public int DataBits
        {
            get { return m_dataBits; }
            set { m_dataBits = value; }
        }

        int m_baudRate = 9600;
        public int BaudRate
        {
            get { return m_baudRate; }
            set { m_baudRate = value; }
        }

        Parity m_parity;
        public Parity Parity
        {
            get { return m_parity; }
            set { m_parity = value; }
        }

        StopBits m_stopBits = StopBits.One;
        public StopBits StopBits
        {
            get { return m_stopBits; }
            set { m_stopBits = value; }
        }

        bool m_dtr;
        public bool DtrEnable
        {
            get { return m_dtr; }
            set { m_dtr = value; }
        }

        bool m_rts;
        public bool RtsEnable
        {
            get { return m_rts; }
            set { m_rts = value; }
        }

        #endregion

        IMessageReader m_reader;

        public COMReceiver()
        { }

        void Connect()
        {
            m_port = new SerialPort
            {
                PortName = this.PortName,
                BaudRate = this.BaudRate,
                DataBits = this.DataBits,
                StopBits = this.StopBits,
                Parity = this.Parity,
                DtrEnable = this.DtrEnable,
                RtsEnable = this.RtsEnable
            };
            TraceHelper.WriteEvent(this, "Opening port {0} (BaudRate: {1}, Parity: {2}, DataBits: {3}, StopBits: {4}, Dtr: {5}, Rts: {6})", PortName, BaudRate, Parity, DataBits, StopBits, DtrEnable, RtsEnable);
            m_port.Open();
            TraceHelper.WriteEvent(this, "Port is opened");

            m_reader = GetMessageReader();
            m_reader.Stream = new SerialDataStream(m_port);
            m_reader.Initialize();
            OnMessageReceived(new ServiceMessage() { Type = MessageType.Connect });
        }

        protected sealed override void ReceiveProc()
        {
            if (m_reader == null)
            {
                Connect();
            }

            IMessage msg = m_reader.ReadMessage(StopToken);
            if (msg != null)
            {
                msg.Source = SourceName;
                string addr = m_reader.Stream.ToString();
                if (!String.IsNullOrEmpty(addr))
                    msg.Source += ", " + addr;
                if (msg is ObjectMessage)
                    (msg as ObjectMessage).Pult = Pult;
                OnMessageReceived(msg);
                m_reader.WriteAnswer(msg, true);
            }
            else
            {
                // Конец
                TraceHelper.WriteEvent(this, "Closing port");
                m_reader.Stream.Close();
                m_reader = null;
                OnMessageReceived(new ServiceMessage() {Type = MessageType.Disconnect});
            }
        }

        protected override void StopInternal()
        {
            base.StopInternal();

            if (m_port != null)
            {
                m_port.Close();
                m_port = null;
            }
        }
    }
}
