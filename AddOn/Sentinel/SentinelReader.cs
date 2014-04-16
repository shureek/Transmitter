using System;
using System.Collections.Generic;

namespace KSoft.AddOn
{
    public class SentinelReader
        : IMessageReader
    {
        static readonly byte[] Ending = new byte[] { 0x0a, 0x0d };

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
                // Должно быть сообщение вида
                // 33 30 30 33 31 2D 2D 2D 2D 53 46 0A 0D
                //     1           5           9       12
                //    [№ объекта]             [Код] END
                if (bytes.Count != 13)
                    throw new MessageFormatException("Неверная длина сообщения", bytes);

                var msg = new ObjectMessage();
                var encoding = System.Text.Encoding.ASCII;
                try
                {
                    msg.ObjectNumber = encoding.GetString(bytes, 1, 4).TrimStart('0');
                    msg.Code = encoding.GetString(bytes, 9, 2);
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
            // Ответ не отправляется
        }

        public void Initialize()
        {
            // Ничего не делается при инициализации
        }
    }
}
