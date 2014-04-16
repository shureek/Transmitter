using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KSoft
{
    /// <summary>
    /// Базовый интерфейс объекта, получающего сообщения.
    /// </summary>
    public interface IReceiver
    {
        /// <summary>
        /// Событие при получении сообщения.
        /// </summary>
        event EventHandler<MessageEventArgs> MessageReceived;

        /// <summary>
        /// Событие при ошибке.
        /// </summary>
        event EventHandler<ErrorEventArgs> ErrorOccured;

        /// <summary>
        /// Объект, отвечающий за создание объектов IMessageReader.
        /// </summary>
        IObjectFactory<IMessageReader> MessageReaderFactory { get; set; }

        /// <summary>
        /// Название источника для записи в сообщения.
        /// </summary>
        string SourceName { get; set; }

        /// <summary>
        /// Уникальный идентификатор получателя (для логов).
        /// </summary>
        Guid ID { get; set; }

        /// <summary>
        /// Пульт для записи в сообщение.
        /// </summary>
        string Pult { get; set; }

        /// <summary>
        /// Запуск получателя.
        /// </summary>
        void Start();

        /// <summary>
        /// Остановка получателя.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// Базовый интерфейс объекта, отправляющего сообщения.
    /// </summary>
    public interface ISender
    {
        /// <summary>
        /// Отправка сообщения (синхронная).
        /// </summary>
        /// <param name="msg">Сообщение для отправки.</param>
        void SendMessage(IMessage msg);

        /// <summary>
        /// Отправка сообщения (асинхронная).
        /// </summary>
        /// <param name="msg">Сообщение для отправки.</param>
        /// <param name="cancellationToken">Token for cancelling pending operation.</param>
        /// <returns>Task</returns>
        Task SendMessageAsync(IMessage msg, CancellationToken cancellationToken);

        /// <summary>
        /// Отправка сообщения (асинхронная).
        /// </summary>
        /// <param name="msg">Сообщение для отправки.</param>
        /// <returns>Task</returns>
        Task SendMessageAsync(IMessage msg);
    }

    /// <summary>
    /// Базовый интерфейс объекта, отвечающего за чтение сообщения из потока.
    /// </summary>
    public interface IMessageReader
    {
        /// <summary>
        /// Поток.
        /// </summary>
        DataStream Stream { get; set; }

        /// <summary>
        /// Начало обмена.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Чтение сообщения из потока.
        /// </summary>
        /// <param name="cancellationToken">Объект для отмены чтения сообщения.</param>
        /// <returns>Прочитанное сообщение.</returns>
        IMessage ReadMessage(CancellationToken cancellationToken);

        /// <summary>
        /// Отправить ответ (подтверждение о получении).
        /// </summary>
        /// <param name="receivedMessage">Полученное сообщение, на которое нужно отправить ответ.</param>
        /// <param name="ok"><value>true</value>, если сообщение успешно отработано.</param>
        void WriteAnswer(IMessage receivedMessage, bool ok);
    }

    public interface IObjectFactory<T>
        where T: class
    {
        T Get();
        void Release(T obj);
    }

    /// <summary>
    /// Базовый интерфейс сообщения.
    /// </summary>
    public interface IMessage
    {
        object Id { get; }
        DateTime Created { get; }
        object Body { get; }
        string Source { get; set; }
    }
}
