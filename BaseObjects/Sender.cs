using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
