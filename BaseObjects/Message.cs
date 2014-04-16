using System;
using System.Text;

namespace KSoft
{
    /// <summary>
    /// Базовый класс сообщения.
    /// </summary>
    public class Message
        : IMessage
    {
        string m_source;
        DateTime m_created;
        object m_id;
        MessageType m_type;

        public Message()
        {
            m_id = Guid.NewGuid();
            m_created = DateTime.Now;
        }

        public string Source
        {
            get { return m_source; }
            set { m_source = value; }
        }

        /// <summary>
        /// Дата создания сообщения (get, set).
        /// </summary>
        public DateTime Created
        {
            get { return m_created; }
            set { m_created = value; }
        }

        public object Id
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public MessageType Type
        {
            get { return m_type; }
            set { m_type = value; }
        }

        public virtual object Body
        {
            get { return ToString(); }
        }
    }

// ReSharper disable InconsistentNaming
    public class ObjectMessage
// ReSharper restore InconsistentNaming
        : Message
    {
        string m_pult;
        string m_objectNumber;
        string m_zone;
        string m_cable;
        string m_code;
        DateTime m_objDate;

        public DateTime ObjectDate
        {
            get { return m_objDate; }
            set { m_objDate = value; }
        }

        public string Pult
        {
            get { return m_pult; }
            set { m_pult = value; }
        }

        public string ObjectNumber
        {
            get { return m_objectNumber; }
            set { m_objectNumber = value; }
        }

        public string Zone
        {
            get { return m_zone; }
            set { m_zone = value; }
        }

        public string Cable
        {
            get { return m_cable; }
            set { m_cable = value; }
        }

        public string Code
        {
            get { return m_code; }
            set { m_code = value; }
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new StringBuilder();

            sb.AppendNotEmpty("Pult {0}", Pult, "; ");
            sb.AppendNotEmpty("Obj {0}", ObjectNumber, "; ");
            sb.AppendNotEmpty("Zone {0}", Zone, "; ");
            sb.AppendNotEmpty("Cable {0}", Cable, "; ");
            sb.AppendNotEmpty("Code {0}", Code, "; ");

            return sb.ToString();
        }
    }

    public class ServiceMessage
        : Message
    {
        public override string ToString()
        {
            return this.Type.ToString();
        }

        public override object Body
        {
            get { return this.Type; }
        }
    }

    public enum MessageType
    {
        Unknown = 0,
        Event,
        Test,
        Connect,
        Disconnect
    }
}
