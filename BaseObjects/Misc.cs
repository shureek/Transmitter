using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace KSoft
{
    //public delegate void VoidFunc<in T1, in T2>(T1 arg1, T2 arg2);

    public class MessageEventArgs
        : EventArgs
    {
        public IMessage Message { get; set; }
    }

    public class ErrorEventArgs
        : EventArgs
    {
        public Exception Error { get; set; }
    }

    #region [ Exceptions ]

    public class ReceiverException
        : ApplicationException
    {
        object m_sourceAddress;
        public object SourceAddress
        {
            get { return m_sourceAddress; }
            set { m_sourceAddress = value; }
        }

        public ReceiverException()
        { }

        public ReceiverException(string message)
            : base(message)
        { }

        public ReceiverException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public ReceiverException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        { }
    }

    public class NetReceiverException
        : ReceiverException
    {
        System.Net.Sockets.Socket m_socket;
        public System.Net.Sockets.Socket Socket
        {
            get { return m_socket; }
            set { m_socket = value; }
        }

        public NetReceiverException()
        { }

        public NetReceiverException(string message)
            : base(message)
        { }

        public NetReceiverException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public NetReceiverException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        { }
    }

    public class MessageFormatException
        : FormatException
    {
        readonly object m_wrongData;

        public object WrongData
        {
            get { return m_wrongData; }
        }

        public MessageFormatException()
        { }

        public MessageFormatException(string message)
            : base(message)
        { }

        public MessageFormatException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public MessageFormatException(object wrongData)
        {
            m_wrongData = wrongData;
        }

        public MessageFormatException(string message, object wrongData)
            : base(message)
        {
            m_wrongData = wrongData;
        }

        public MessageFormatException(string message, object wrongData, Exception innerException)
            : base(message, innerException)
        {
            m_wrongData = wrongData;
        }
    }

    public class SenderException
        : ApplicationException
    {
        public SenderException()
        { }

        public SenderException(string message)
            : base(message)
        { }

        public SenderException(string message, Exception innerException)
            : base(message, innerException)
        { }

        public SenderException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        { }
    }

    #endregion

    public enum ProcessState
    {
        Stopped = 0,
        Starting,
        Working,
        Stopping
    }

    public struct ArraySegment<T> : IList<T>
    {
        readonly T[] m_array;
        readonly int m_offset;
        readonly int m_count;

        public T[] Array
        {
            get { return m_array; }
        }

        public int Offset
        {
            get { return m_offset; }
        }

        public ArraySegment(T[] array, int offset, int count)
        {
            if (array == null)
                throw new NullReferenceException("Array must not be null");
            if (offset < 0 || offset >= array.Length)
                throw new ArgumentOutOfRangeException("offset", offset, "Offset must be >= 0 and < array.Length");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", count, "Length must be >= 0");
            if (offset + count > array.Length)
                throw new ArgumentOutOfRangeException("count", "offset + count must be <= array.Length");

            m_array = array;
            m_offset = offset;
            m_count = count;
        }

        public int IndexOf(T item)
        {
            int index = System.Array.IndexOf(m_array, item, m_offset, m_count);
            return index == -1 ? -1 : index - m_offset;
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public T this[int index]
        {
            get { return m_array[index + m_offset]; }
            set
            {
                throw new NotSupportedException();
            }
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_array.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return m_count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class Enumerator : IEnumerator<T>
        {
            readonly ArraySegment<T> m_segment;
            int m_index;

            public Enumerator(ArraySegment<T> segment)
            {
                m_segment = segment;
                m_index = -1;
            }

            public T Current
            {
                get { return m_segment[m_index]; }
            }

            public void Dispose()
            {
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                m_index++;
                return m_index < m_segment.Count;
            }

            public void Reset()
            {
                m_index = -1;
            }
        }
    }

    public static class TraceHelper
    {
        static Guid DefaultGuid;

        public static void Initialize()
        {
            DefaultGuid = Trace.CorrelationManager.ActivityId;

            foreach (TraceListener listener in Trace.Listeners)
            {
                //listener.
            }
        }

        static string ObjectName(object obj)
        {
            if (obj == null)
                return "null";
            if (obj is IReceiver)
            {
                var rec = obj as IReceiver;
                return String.Format("{0} ({1}, {2})", rec.GetType().FullName, rec.ID, rec.SourceName);
            }
            return obj.ToString();
        }

        public static void StartOperation()
        {
            Trace.CorrelationManager.StartLogicalOperation();
        }

        public static void WriteEvent(object source, string format, params object[] arg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ObjectName(source));
            sb.Append(": ");
            sb.AppendFormat(format, arg);
            Trace.TraceInformation(sb.ToString());
        }

        public static void WriteError(object source, Exception error)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ObjectName(source));
            sb.Append(": ");
            AppendException(error, sb);
            Trace.TraceError(sb.ToString());
        }

        public static void WriteError(object source, string format, params object[] arg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ObjectName(source));
            sb.Append(": ");
            sb.AppendFormat(format, arg);
            Trace.TraceError(sb.ToString());
        }

        public static void WriteWarning(object source, string format, params object[] arg)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ObjectName(source));
            sb.Append(": ");
            sb.AppendFormat(format, arg);
            Trace.TraceWarning(sb.ToString());
        }

        static void AppendException(Exception ex, StringBuilder sb)
        {
            Type exType = ex.GetType();
            sb.AppendFormat("({0}) {1}", exType.FullName, ex.Message);
            sb.AppendLine();
            bool addSeparator = false;
            foreach (var prop in exType.GetProperties())
            {
                if (addSeparator)
                    sb.Append("; ");
                object value = prop.GetValue(ex, new object[0]);
                string strValue;
                if (value == null)
                    continue;
                if (value is IList<byte>)
                    strValue = (value as IList<byte>).ToHexString();
                else
                    strValue = value.ToString();
                sb.AppendFormat("{0}: {1}", prop.Name, strValue);
                addSeparator = true;
            }
            sb.AppendLine();
            if (ex.InnerException != null)
            {
                sb.Append("Inner exception: ");
                AppendException(ex.InnerException, sb);
            }
        }
    }
}