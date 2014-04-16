using System;
using System.Collections.Generic;
using System.IO;

namespace KSoft.AddOn
{
    public class RitmReader
        : IMessageReader
    {
        static readonly byte[] Ending = new byte[] { 0x14 };

        public RitmReader()
        { }

        public void Initialize() { }

        DataStream m_stream;
        public DataStream Stream
        {
            get { return m_stream; }
            set { m_stream = value; }
        }

        public IMessage ReadMessage(System.Threading.CancellationToken cancellationToken)
        {
            IList<byte> bytes;
            if (Stream.ReadTo(Ending, out bytes))
            {
                // Обрежем начальные 0x06
                int _06count = 0;
                while (bytes[_06count] == 0x06)
                    _06count++;
                if (_06count > 0)
                    bytes = bytes.GetSegment(_06count);

                // Должно быть сообщение вида
                // 35 30 31 31 20 31 38 30 30 33 36 45 31 32 30 30 32 30 30 32 14
                //  5  0  1  1     1  8  0  0  3  6  E  1  2  0  0  2  0  0  2 END
                //                       7          11          15    17
                //                      [№ объекта] [   Код   ] [Зон] [Шлейф ] END
                //  1  0  1  1                                   @             END - тестовое сообщение
                if (bytes.Count != 21)
                    throw new MessageFormatException("Неверная длина сообщения", bytes);

                IMessage msg;
                var encoding = System.Text.Encoding.ASCII;
                try
                {
                    string sig = encoding.GetString(bytes, 0, 4);
                    if (sig == "5011")
                    {
                        msg = new ObjectMessage
                            {
                                ObjectNumber = encoding.GetString(bytes, 7, 4).Replace('A', '0').TrimStart('0'),
                                Code = encoding.GetString(bytes, 11, 4).Replace('A', '0'),
                                Zone = encoding.GetString(bytes, 15, 2).Replace('A', '0').TrimStart('0'),
                                Cable = encoding.GetString(bytes, 17, 3).Replace('A', '0').TrimStart('0'),
                                Type = MessageType.Event
                            };
                    }
                    else if (sig == "1011")
                    {
                        msg = new ServiceMessage()
                            {
                                Type = MessageType.Test
                            };
                    }
                    else
                    {
                        throw new MessageFormatException("Ошибка разбора сообщения", bytes);
                    }
                }
                catch (ArgumentException ex)
                {
                    throw new MessageFormatException("Ошибка разбора сообщения", bytes, ex);
                }

                return msg;
            }
            else
            {
                if (bytes.Count > 0)
                    throw new MessageFormatException("Не найден признак окончания сообщения", bytes);
                return null;
            }
        }

        public void WriteAnswer(IMessage receivedMessage, bool ok)
        {
            if (ok)
            {
                Stream.WriteByte(0x06);
                Stream.Flush();
            }
        }
    }
}
