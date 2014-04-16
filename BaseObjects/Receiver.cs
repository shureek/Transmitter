using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace KSoft
{
    public abstract class ReceiverBase
        : IReceiver
    {
        ProcessState m_state;

        /// <summary>
        /// Текущее состояние получателя (get).
        /// </summary>
        public ProcessState State
        {
            get { return m_state; }
            private set { m_state = value; }
        }

        Guid m_id;
        public Guid ID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        /// <summary>
        /// Объект для синхронизации между потоками.
        /// </summary>
        readonly object m_syncObj;

        /// <summary>
        /// Событие при получении сообщения.
        /// </summary>
        public event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// Событие при ошибке.
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccured;

        protected IMessageReader GetMessageReader()
        {
            var reader = MessageReaderFactory.Get();
            Debug.Assert(reader != null, "MessageReader не получен");
            return reader;
        }

        protected void ReleaseMessageReader(IMessageReader reader)
        {
            reader.Stream = null;
            MessageReaderFactory.Release(reader);
        }

        protected ReceiverBase()
        {
            m_id = Guid.NewGuid();
            m_syncObj = new Object();
            State = ProcessState.Stopped;
        }

        /// <summary>
        /// Запуск получателя.
        /// </summary>
        public void Start()
        {
            if (State != ProcessState.Stopped)
                throw new InvalidOperationException("Перед запуском получатель должен быть остановлен");

            lock(m_syncObj)
            {
                if (State != ProcessState.Stopped)
                    throw new InvalidOperationException("Перед запуском получатель должен быть остановлен");

                State = ProcessState.Starting;
                StartInternal();
                State = ProcessState.Working;
            }
        }

        protected abstract void StartInternal();

        /// <summary>
        /// Остановка получателя.
        /// </summary>
        public void Stop()
        {
            ProcessState st = State;
            if (st != ProcessState.Working && st != ProcessState.Starting)
                return;

            lock(m_syncObj)
            {
                if (State == ProcessState.Working)
                {
                    State = ProcessState.Stopping;
                    StopInternal();
                    State = ProcessState.Stopped;
                }
            }
        }

        protected abstract void StopInternal();

        #region [ Private methods ]
        
        protected void OnMessageReceived(MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageReceived;
            if (handler != null)
                handler(this, e);
        }

        protected void OnMessageReceived(IMessage msg)
        {
            OnMessageReceived(new MessageEventArgs() { Message = msg });
        }

        protected void OnErrorOccured(ErrorEventArgs e)
        {
        }

        protected void OnErrorOccured(Exception error)
        {
            OnErrorOccured(new ErrorEventArgs() { Error = error });
        }

        protected void OnErrorOccured(object source, ErrorEventArgs e)
        {
            EventHandler<ErrorEventArgs> handler = ErrorOccured;
            if (handler != null)
                handler(source, e);
        }

        protected void OnErrorOccured(object source, Exception error)
        {
            OnErrorOccured(source, new ErrorEventArgs() { Error = error });
        }

        #endregion

        string m_sourceName;
        public string SourceName
        {
            get { return m_sourceName; }
            set { m_sourceName = value; }
        }

        string m_pult;
        public string Pult
        {
            get { return m_pult; }
            set { m_pult = value; }
        }

        IObjectFactory<IMessageReader> m_messageReaderFactory;
        public IObjectFactory<IMessageReader> MessageReaderFactory
        {
            get { return m_messageReaderFactory; }
            set { m_messageReaderFactory = value; }
        }
    }

    public abstract class AsyncReceiver
        : ReceiverBase
    {
        Thread m_workThread;
        CancellationTokenSource m_stopTokenSource;

        protected CancellationToken StopToken
        {
            get { return m_stopTokenSource.Token; }
        }

        protected override void StartInternal()
        {
            m_stopTokenSource = new CancellationTokenSource();
            m_workThread = new Thread(StartWorkProcess);
            m_workThread.Start();
        }

        protected override void StopInternal()
        {
            m_stopTokenSource.Cancel();
            m_workThread.Join();
            m_workThread = null;
        }

        void StartWorkProcess()
        {
            Trace.CorrelationManager.ActivityId = Guid.NewGuid(); // new guid

            // Поток может запуститься до того, как пройдет инициализация
            while (State == ProcessState.Starting)
                Thread.Sleep(100);

            while (State == ProcessState.Working)
            {
                try
                {
                    ReceiveProc();
                }
                catch (Exception ex)
                {
                    OnErrorOccured(ex);
                }
            }
        }

        protected abstract void ReceiveProc();
    }
}
