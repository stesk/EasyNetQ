using System;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Interception;
using EasyNetQ.Internals;
using EasyNetQ.Persistent;
using EasyNetQ.Topology;
using RabbitMQ.Client;

namespace EasyNetQ;

/// <summary>
///     Represents a result of a pull
/// </summary>
public interface IPullResult : IDisposable
{
    /// <summary>
    ///     True if a message is available
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    ///     Returns remained messages count if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ulong MessagesCount { get; }

    /// <summary>
    ///     Returns received info if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public MessageReceivedInfo ReceivedInfo { get; }
}

/// <summary>
///     Represents a result of a message pull
/// </summary>
public readonly struct PullResult : IPullResult
{
    private readonly MessageReceivedInfo? receivedInfo;
    private readonly MessageProperties? properties;
    private readonly ReadOnlyMemory<byte> body;
    private readonly ulong messagesCount;
    private readonly IDisposable? disposable;

    /// <summary>
    ///     Represents a result when no message is available
    /// </summary>
    public static PullResult NotAvailable { get; } = new(false, 0, null, null, null, null);

    /// <summary>
    ///     Represents a result when a message is available
    /// </summary>
    /// <returns></returns>
    public static PullResult Available(
        ulong messagesCount,
        MessageReceivedInfo receivedInfo,
        MessageProperties properties,
        in ReadOnlyMemory<byte> body,
        IDisposable? disposable
    )
    {
        return new PullResult(true, messagesCount, receivedInfo, properties, body, disposable);
    }

    private PullResult(
        bool isAvailable,
        ulong messagesCount,
        MessageReceivedInfo? receivedInfo,
        MessageProperties? properties,
        in ReadOnlyMemory<byte> body,
        IDisposable? disposable
    )
    {
        IsAvailable = isAvailable;
        this.messagesCount = messagesCount;
        this.receivedInfo = receivedInfo;
        this.properties = properties;
        this.body = body;
        this.disposable = disposable;
    }

    /// <summary>
    ///     True if a message is available
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    ///     Returns remained messages count if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ulong MessagesCount
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return messagesCount;
        }
    }

    /// <summary>
    ///     Returns received info if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public MessageReceivedInfo ReceivedInfo
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return receivedInfo!;
        }
    }

    /// <summary>
    ///     Returns properties if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public MessageProperties Properties
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return properties!;
        }
    }

    /// <summary>
    ///     Returns body info if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ReadOnlyMemory<byte> Body
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return body;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposable?.Dispose();
    }
}

/// <summary>
///     Represents a result of a message pull
/// </summary>
public readonly struct PullResult<T> : IPullResult
{
    private readonly MessageReceivedInfo? receivedInfo;
    private readonly IMessage<T>? message;
    private readonly ulong messagesCount;

    /// <summary>
    ///     Represents a result when no message is available
    /// </summary>
    public static PullResult<T> NotAvailable { get; } = new(false, 0, null, null);

    /// <summary>
    ///     Represents a result when a message is available
    /// </summary>
    /// <returns></returns>
    public static PullResult<T> Available(
        ulong messagesCount, MessageReceivedInfo receivedInfo, IMessage<T> message
    )
    {
        return new PullResult<T>(true, messagesCount, receivedInfo, message);
    }

    private PullResult(
        bool isAvailable,
        ulong messagesCount,
        MessageReceivedInfo? receivedInfo,
        IMessage<T>? message
    )
    {
        IsAvailable = isAvailable;
        this.messagesCount = messagesCount;
        this.receivedInfo = receivedInfo;
        this.message = message;
    }

    /// <summary>
    ///     True if a message is available
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    ///     Returns remained messages count if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public ulong MessagesCount
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return messagesCount;
        }
    }

    /// <summary>
    ///     Returns received info if the message is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public MessageReceivedInfo ReceivedInfo
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return receivedInfo!;
        }
    }

    /// <summary>
    ///     Returns message if it is available
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public IMessage<T> Message
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("No message is available");

            return message!;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}


/// <summary>
///     Allows to receive messages by pulling them one by one
/// </summary>
public interface IPullingConsumer<TPullResult> : IDisposable where TPullResult : IPullResult
{
    /// <summary>
    ///     Receives a single message
    /// </summary>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns></returns>
    Task<TPullResult> PullAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Acknowledges one or more messages
    /// </summary>
    /// <param name="deliveryTag"></param>
    /// <param name="multiple"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task AckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Rejects one or more messages
    /// </summary>
    /// <param name="deliveryTag"></param>
    /// <param name="multiple"></param>
    /// <param name="requeue"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task RejectAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default);
}

/// <summary>
///     Represent pulling consumer options
/// </summary>
public readonly struct PullingConsumerOptions
{
    /// <summary>
    ///     True if auto ack is enabled for the consumer
    /// </summary>
    public bool AutoAck { get; }

    /// <summary>
    ///     Operations timeout
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    ///     Creates PullingConsumerOptions
    /// </summary>
    /// <param name="autoAck">The autoAck</param>
    /// <param name="timeout">The timeout</param>
    public PullingConsumerOptions(bool autoAck, TimeSpan timeout)
    {
        AutoAck = autoAck;
        Timeout = timeout;
    }
}

/// <inheritdoc />
public class PullingConsumer : IPullingConsumer<PullResult>
{
    private readonly IPersistentChannel channel;
    private readonly IProduceConsumeInterceptor[] produceConsumeInterceptors;
    private readonly PullingConsumerOptions options;
    private readonly Queue queue;

    /// <summary>
    ///     Creates PullingConsumer
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="queue">The queue</param>
    /// <param name="channel">The channel</param>
    /// <param name="produceConsumeInterceptors">The produce-consumer interceptors</param>
    public PullingConsumer(
        in PullingConsumerOptions options,
        in Queue queue,
        IPersistentChannel channel,
        IProduceConsumeInterceptor[] produceConsumeInterceptors
    )
    {
        this.queue = queue;
        this.options = options;
        this.channel = channel;
        this.produceConsumeInterceptors = produceConsumeInterceptors;
    }

    /// <inheritdoc />
    public async Task<PullResult> PullAsync(CancellationToken cancellationToken = default)
    {
        using var cts = cancellationToken.WithTimeout(options.Timeout);

        var basicGetResult = await channel.InvokeChannelActionAsync<BasicGetResult?, BasicGetAction>(
            new BasicGetAction(queue, options.AutoAck), cts.Token
        ).ConfigureAwait(false);

        if (basicGetResult == null)
            return PullResult.NotAvailable;

        var messagesCount = basicGetResult.MessageCount;
        var messageProperties = new MessageProperties();
        messageProperties.CopyFrom(basicGetResult.BasicProperties);
        var messageReceivedInfo = new MessageReceivedInfo(
            "",
            basicGetResult.DeliveryTag,
            basicGetResult.Redelivered,
            basicGetResult.Exchange,
            basicGetResult.RoutingKey,
            queue.Name
        );
        var message = new ConsumedMessage(messageReceivedInfo, messageProperties, basicGetResult.Body);
        var interceptedMessage = produceConsumeInterceptors.OnConsume(message);
        return PullResult.Available(
            messagesCount,
            interceptedMessage.ReceivedInfo,
            interceptedMessage.Properties,
            interceptedMessage.Body,
            null
        );
    }

    /// <inheritdoc />
    public async Task AckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
    {
        if (options.AutoAck)
            throw new InvalidOperationException("Cannot ack in auto ack mode");

        using var cts = cancellationToken.WithTimeout(options.Timeout);

        await channel.InvokeChannelActionAsync<NoResult, BasicAckAction>(
            new BasicAckAction(deliveryTag, multiple), cts.Token
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RejectAsync(
        ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default
    )
    {
        if (options.AutoAck)
            throw new InvalidOperationException("Cannot reject in auto ack mode");

        using var cts = cancellationToken.WithTimeout(options.Timeout);

        await channel.InvokeChannelActionAsync<NoResult, BasicNackAction>(
            new BasicNackAction(deliveryTag, multiple, requeue), cts.Token
        ).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        channel.Dispose();
    }

    private readonly struct BasicGetAction : IPersistentChannelAction<BasicGetResult?>
    {
        private readonly Queue queue;
        private readonly bool autoAck;

        public BasicGetAction(in Queue queue, bool autoAck)
        {
            this.queue = queue;
            this.autoAck = autoAck;
        }

        public BasicGetResult? Invoke(IModel model) => model.BasicGet(queue.Name, autoAck);
    }

    private readonly struct BasicAckAction : IPersistentChannelAction<NoResult>
    {
        private readonly ulong deliveryTag;
        private readonly bool multiple;

        public BasicAckAction(ulong deliveryTag, bool multiple)
        {
            this.deliveryTag = deliveryTag;
            this.multiple = multiple;
        }

        public NoResult Invoke(IModel model)
        {
            model.BasicAck(deliveryTag, multiple);
            return NoResult.Instance;
        }
    }

    private readonly struct BasicNackAction : IPersistentChannelAction<NoResult>
    {
        private readonly ulong deliveryTag;
        private readonly bool multiple;
        private readonly bool requeue;

        public BasicNackAction(ulong deliveryTag, bool multiple, bool requeue)
        {
            this.deliveryTag = deliveryTag;
            this.multiple = multiple;
            this.requeue = requeue;
        }

        public NoResult Invoke(IModel model)
        {
            model.BasicNack(deliveryTag, multiple, requeue);
            return NoResult.Instance;
        }
    }
}

/// <inheritdoc />
public class PullingConsumer<T> : IPullingConsumer<PullResult<T>>
{
    private readonly IPullingConsumer<PullResult> consumer;
    private readonly IMessageSerializationStrategy messageSerializationStrategy;

    /// <summary>
    ///     Creates PullingConsumer
    /// </summary>
    public PullingConsumer(
        IPullingConsumer<PullResult> consumer, IMessageSerializationStrategy messageSerializationStrategy
    )
    {
        this.consumer = consumer;
        this.messageSerializationStrategy = messageSerializationStrategy;
    }

    /// <inheritdoc />
    public async Task<PullResult<T>> PullAsync(CancellationToken cancellationToken = default)
    {
        var pullResult = await consumer.PullAsync(cancellationToken).ConfigureAwait(false);
        if (!pullResult.IsAvailable)
        {
            pullResult.Dispose();
            return PullResult<T>.NotAvailable;
        }

        var message = messageSerializationStrategy.DeserializeMessage(pullResult.Properties, pullResult.Body);
        if (typeof(T).IsAssignableFrom(message.MessageType))
            return PullResult<T>.Available(
                pullResult.MessagesCount,
                pullResult.ReceivedInfo,
                new Message<T>((T?)message.GetBody(), message.Properties)
            );

        throw new EasyNetQException(
            $"Incorrect message type returned. Expected {typeof(T).Name}, but was {message.MessageType.Name}"
        );
    }

    /// <inheritdoc />
    public Task AckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default)
    {
        return consumer.AckAsync(deliveryTag, multiple, cancellationToken);
    }

    /// <inheritdoc />
    public Task RejectAsync(
        ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default
    )
    {
        return consumer.RejectAsync(deliveryTag, multiple, requeue, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        consumer.Dispose();
    }
}
