using System;
using System.Collections.Generic;

namespace KSoft
{
    /// <summary>
    /// Объект, отвечающий за передачу сообщений от приемников передатчикам.
    /// </summary>
    public sealed class Transmitter
        : IDisposable
    {
        readonly List<IReceiver> m_receivers;
        readonly List<ISender> m_senders;

        public event EventHandler<MessageEventArgs> MessageReceived;
        public event EventHandler<MessageEventArgs> MessageSent;
        public event EventHandler<ErrorEventArgs> ErrorOccured;

        public Transmitter()
        {
            m_receivers = new List<IReceiver>();
            m_senders = new List<ISender>();
        }

        public void Start()
        {
            foreach (var receiver in m_receivers)
            {
                receiver.Start();
            }
            TraceHelper.WriteEvent(this, "Receivers started");
        }

        public void Stop()
        {
            TraceHelper.WriteError(this, "Stopping receivers");
            foreach (var receiver in m_receivers)
            {
                receiver.Stop();
            }
        }

        bool ProcessMessage(IMessage msg)
        {
            if (msg is ObjectMessage)
                if ((msg as ObjectMessage).Type != MessageType.Event)
                    return false;
            bool result = true;
            foreach (var sender in m_senders)
            {
                try
                {
                    sender.SendMessage(msg);
                }
                catch (Exception ex)
                {
                    TraceHelper.WriteError(sender, ex);
                    OnErrorOccured(sender, new ErrorEventArgs() { Error = ex });
                    result = false;
                }
            }
            return result;
        }

        void ReceiverOnErrorOccured(object sender, ErrorEventArgs e)
        {
            TraceHelper.WriteError(sender, e.Error);
            OnErrorOccured(sender, e);
        }

        void ReceiverOnMessageReceived(object sender, MessageEventArgs e)
        {
            OnMessageReceived(sender, e);
            if (ProcessMessage(e.Message))
                OnMessageSent(this, e);
        }

        public void AddReceiver(IReceiver receiver)
        {
            m_receivers.Add(receiver);
            receiver.MessageReceived += ReceiverOnMessageReceived;
            receiver.ErrorOccured += ReceiverOnErrorOccured;
            TraceHelper.WriteEvent(this, "Receiver added ({0}, SourceName: {1}, Pult: {2})", receiver.ID, receiver.SourceName, receiver.Pult);
        }

        public void AddSender(ISender sender)
        {
            m_senders.Add(sender);
        }

        void OnMessageReceived(object sender, MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageReceived;
            if (handler != null)
                handler(sender, e);
        }

        void OnMessageSent(object sender, MessageEventArgs e)
        {
            EventHandler<MessageEventArgs> handler = MessageSent;
            if (handler != null)
                handler(sender, e);
        }

        void OnErrorOccured(object sender, ErrorEventArgs e)
        {
            EventHandler<ErrorEventArgs> handler = ErrorOccured;
            if (handler != null)
                handler(sender, e);
        }

        public void Dispose()
        {
            foreach (IReceiver receiver in m_receivers)
            {
                receiver.MessageReceived -= ReceiverOnMessageReceived;
                receiver.ErrorOccured -= ReceiverOnErrorOccured;
            }
            m_receivers.Clear();
            m_senders.Clear();
        }
    }
}
