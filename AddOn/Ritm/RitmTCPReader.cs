using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KSoft.AddOn
{
    public class RitmTCPReader
        : IMessageReader
    {
        DataStream m_stream;
        public DataStream Stream
        {
            get { return m_stream; }
            set { m_stream = value; }
        }

        string m_objectNumber;
        public string ObjectNumber
        {
            get { return m_objectNumber; }
        }

        static readonly byte[] _0d = new byte[] { 0x0d };
        static readonly byte[] _0d0a = new byte[] { 0x0d, 0x0a };
        static readonly byte[] _testAnswer = new byte[] { 0x42, 0x0d, 0x0d, 0x0a };
        //int m_bufferCount;
        byte[] m_prefix = null;
        byte[] m_suffix = null;
        //string m_acknowledgement;

        void SetPrefix(string value)
        {
            SetPrefix(Stream.Encoding.GetBytes(value));
        }

        void SetPrefix(byte[] value)
        {
            m_prefix = value;
        }

        void SetSuffix(byte[] value)
        {
            m_suffix = value;
        }

        void SetSuffix(string value)
        {
            SetSuffix(Stream.Encoding.GetBytes(value));
        }

        public RitmTCPReader()
        { }

        public void Initialize()
        {
            Stream.SetNewLine(_0d);
            Stream.Encoding = System.Text.Encoding.ASCII;
            Stream.PrepareBuffer();

            SetPrefix(_0d);
            SetSuffix(_0d0a);

            try
            {
                // Сначала отправляем сообщение о готовности
                WriteLine("READY");
                WriteLine("READY");
                WriteLine("READY");

                SetSuffix(_0d);

                // Запрос номера объекта
                string answer;
                answer = Request("+o");
                m_objectNumber = answer.TrimStart('0');
                TraceHelper.WriteEvent(Stream, "Номер объекта: {0}", m_objectNumber);

                // Запрос пароля
                answer = Request("+ps"); // TestTest

                // Запрос версии
                answer = Request("+v"); // что-то типа VER 01.003.054

                // Запрос каких-то флагов
                answer = Request("+R"); // что-то типа 0111011000000000

                // Запрос чего-то еще
                answer = Request("+FP"); // что-то типа C0000 S0000

                // Запрос событий
                answer = Request("+gt"); // null
            }
            finally
            {
                Stream.ReleaseBuffer();
            }
        }

        string Request(string question)
        {
            WriteLine(question);
            string answer;
            try
            {
                IList<byte> bytes;
                answer = ReadAnswer(out bytes);
            }
            catch (MessageFormatException ex)
            {
                throw new MessageFormatException(String.Format("Неверный ответ от сервера ({0}): {1}", question, ex.Data), ex);
            }
            TraceHelper.WriteEvent(Stream, "{0}: {1}", question, answer);
            return answer;
        }

        string ReadAnswer(out IList<byte> bytes)
        {
            StringBuilder sb = new StringBuilder();
            List<byte> bytesList = new List<byte>();
            bytes = bytesList;
            while (true)
            {
                var answer = Stream.ReadTo(_0d);
                //if (bytesList.Capacity < bytesList.Count + answer.Count)
                //    bytesList.Capacity = bytesList.Count + answer.Count;
                //bytesList.AddRange(answer);
                foreach (byte b in answer)
                    bytesList.Add(b);
                string answerStr = Stream.Encoding.GetString(answer, 0, answer.Count - _0d.Length);
                if (answerStr == "A")
                {
                    if (sb.Length > 0)
                        sb.AppendLine();
                    sb.Append(answerStr);
                }
                if (answerStr == "OK" || answerStr == "A")
                    return sb.Length > 0 ? sb.ToString() : null;
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(answerStr);
            }
            /*if (answer.Count != 2 && answer.Count != 1)
                throw new MessageFormatException("Неверное сообщение от объекта", answer.ToSeparatedString());
            if (answer[answer.Count - 1] != "OK")
                throw new MessageFormatException("Неверное сообщение от объекта", answer.ToSeparatedString());
            return answer.Count == 2 ? answer[0] : null;*/
        }

        public IMessage ReadMessage(System.Threading.CancellationToken cancellationToken)
        {
            IList<byte> bytes;
            string str = ReadAnswer(out bytes);
            // EVENT: 16:11:55 15/04/13 0208181130040098 000016E6 74
            //        7       16       25        35     42       51

            if (str == null)
                throw new MessageFormatException("Пустое сообщение");

            if (str == "A")
            {
                // Это тест
                var msg = new ObjectMessage()
                    {
                        Type = MessageType.Test,
                        ObjectNumber = m_objectNumber
                    };
                return msg;
            }
            else if (str.Substring(0, 6) == "EVENT:")
            {
                TraceHelper.WriteEvent(Stream, "Message received: {0}", str);

                var msg = new ObjectMessage()
                    {
                        Type = MessageType.Event
                    };

                // Дата
                int hour = Int32.Parse(str.Substring(7, 2));
                int minute = Int32.Parse(str.Substring(10, 2));
                int second = Int32.Parse(str.Substring(13, 2));
                int day = Int32.Parse(str.Substring(16, 2));
                int month = Int32.Parse(str.Substring(19, 2));
                int year = Int32.Parse(str.Substring(22, 2));
                msg.ObjectDate = new DateTime(2000 + year, month, day, hour, minute, second);

                var difference = msg.Created - msg.ObjectDate;

                string objectNumber = str.Substring(25, 4).TrimStart('0');
                if (objectNumber != m_objectNumber)
                    throw new MessageFormatException(
                        String.Format(
                                      "Номер объекта в сообщении ({0}) не совпадает с номером объекта при инициализации обмена {1}",
                            objectNumber, m_objectNumber), str);
                msg.ObjectNumber = objectNumber;
                string type = str.Substring(29, 2);
                if (type != "18")
                    throw new MessageFormatException(String.Format("Неверный признак Contact ID ({0})", type), bytes);
                msg.Code = str.Substring(31, 4);
                char ch1 = msg.Code[0];
                if (ch1 == '1')
                    msg.Code = "E" + msg.Code.Substring(1);
                else if (ch1 == '3')
                    msg.Code = "R" + msg.Code.Substring(1);

                msg.Zone = str.Substring(35, 2);
                msg.Cable = str.Substring(37, 3);

                string ctrl1 = str.Substring(40, 1); // контрольное число 1
                string msgId = str.Substring(42, 8);
                string ctrl2 = str.Substring(51, 2);
                msg.Id = msgId + ctrl2;

                return msg;
            }
            else if (str.Substring(0, 5) == "PACK:")
            {
                // Какое-то запакованное сообщение
                TraceHelper.WriteWarning(Stream, "Packed message: {0}", bytes.ToHexString());
                var msg = new ObjectMessage()
                    {
                        Type = MessageType.Unknown,
                        ObjectNumber = m_objectNumber
                    };
                msg.Id = String.Format("{0:X2}{1:X2}", bytes[5], bytes[6]);
                return msg;
            }

            throw new MessageFormatException("Непонятное сообщение", bytes);
        }

        public void WriteAnswer(IMessage receivedMessage, bool ok)
        {
            if (!(receivedMessage is ObjectMessage))
                return;
            var msg = (ObjectMessage)receivedMessage;
            if (msg.Type == MessageType.Event || msg.Type == MessageType.Unknown)
            {
                if (!(msg.Id is String))
                    return;
                string msgId = (String)msg.Id;
                if (String.IsNullOrEmpty(msgId))
                    return;
                WriteLine("+ACK"+msgId);
            }
            else if (msg.Type == MessageType.Test)
            {
                Stream.Write(_testAnswer);
                Stream.Flush();
            }
        }

        void WriteLine(string str)
        {
            if (m_prefix != null)
                m_stream.Write(m_prefix);
            m_stream.Write(str);
            if (m_suffix != null)
                m_stream.Write(m_suffix);
            m_stream.Flush();
        }
    }
}
