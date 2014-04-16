using System;
using System.Collections.Generic;
using System.Messaging;
using System.Text;

namespace KSoft.AddOn
{
// ReSharper disable InconsistentNaming
    public class MSMQSender
// ReSharper restore InconsistentNaming
        : SenderBase
    {
        string m_queueName;
        public string QueueName
        {
            get { return m_queueName; }
            set { m_queueName = value; }
        }

        MessageQueue m_queue;
        StringBuilder m_sbuilder;

        public MSMQSender()
        {
            m_sbuilder = new StringBuilder();
        }

        public override void SendMessage(IMessage msg)
        {
            if (msg is ObjectMessage)
            {
                var objmsg = (ObjectMessage)msg;
                if (objmsg.Type != MessageType.Event)
                    return;
            }

            EnsureQueueIsOpen();

            // Составляем строку для отправки
            m_sbuilder.Length = 0;
            KSoft.ObjectMessage message = (KSoft.ObjectMessage)msg;
            m_sbuilder.AppendNotEmpty("Plt:{0}", message.Pult, ";");
            m_sbuilder.AppendNotEmpty("Dat:{0}", message.Created, ";");
            m_sbuilder.AppendNotEmpty("Obj:{0}", message.ObjectNumber, ";");
            m_sbuilder.AppendNotEmpty("Msg:{0}", message.Code, ";");
            m_sbuilder.AppendNotEmpty("Zon:{0}", message.Zone, ";");
            m_sbuilder.AppendNotEmpty("Cbl:{0}", message.Cable, ";");

            System.Messaging.Message msmq = new System.Messaging.Message
                {
                    Label = m_sbuilder.ToString(),
                    Recoverable = true,
                    Priority = MessagePriority.AboveNormal
                };
            m_sbuilder.Length = 0;

            m_queue.Send(msmq);
        }

        void EnsureQueueIsOpen()
        {
            if (m_queue == null)
            {
                m_queue = new MessageQueue(m_queueName, false, false, QueueAccessMode.Send);
            }
        }
    }
}