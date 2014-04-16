using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace KSoft
{
    /// <summary>
    /// Базовый класс объекта, отправляющего сообщения.
    /// </summary>
    public abstract class SenderBase : ISender
    {
        public abstract void SendMessage(IMessage msg);

        public virtual Task SendMessageAsync(IMessage msg, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() => SendMessage(msg), TaskCreationOptions.PreferFairness);
        }

        public Task SendMessageAsync(IMessage msg)
        {
            return SendMessageAsync(msg, CancellationToken.None);
        }

        PropertyInfo[] properties = null;

        public override string ToString()
        {
            if (properties == null)
                properties = GetType().GetProperties();

            var sb = new System.Text.StringBuilder(64);
            sb.Append(GetType().Name);
            if (properties.Length > 0)
            {
                sb.Append(" {");
                for (int i = 0; i < properties.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(properties[i].Name);
                    sb.Append(": ");
                    sb.Append(properties[i].GetValue(this, null));
                }
                sb.Append('}');
            }
            return sb.ToString();
        }
    }
}
