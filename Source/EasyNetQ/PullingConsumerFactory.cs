using System.Collections.Generic;
using System.Linq;
using EasyNetQ.Consumer;
using EasyNetQ.Interception;
using EasyNetQ.Persistent;
using EasyNetQ.Topology;

namespace EasyNetQ;

/// <summary>
///     A factory of pulling consumer
/// </summary>
public interface IPullingConsumerFactory
{
    /// <summary>
    ///     Creates a pulling consumer
    /// </summary>
    /// <param name="queue">The queue</param>
    /// <param name="options">The options</param>
    /// <returns></returns>
    IPullingConsumer<PullResult> CreateConsumer(in Queue queue, in PullingConsumerOptions options);

    /// <summary>
    ///     Creates a pulling consumer
    /// </summary>
    /// <param name="queue">The queue</param>
    /// <param name="options">The options</param>
    /// <returns></returns>
    IPullingConsumer<PullResult<T>> CreateConsumer<T>(in Queue queue, in PullingConsumerOptions options);
}

/// <inheritdoc />
public class PullingConsumerFactory : IPullingConsumerFactory
{
    private readonly IConsumerConnection connection;
    private readonly IPersistentChannelFactory channelFactory;
    private readonly IMessageSerializationStrategy messageSerializationStrategy;
    private readonly IProduceConsumeInterceptor[] produceConsumeInterceptors;

    /// <summary>
    ///     Creates PullingConsumerFactory
    /// </summary>
    public PullingConsumerFactory(
        IConsumerConnection connection,
        IPersistentChannelFactory channelFactory,
        IEnumerable<IProduceConsumeInterceptor> produceConsumeInterceptors,
        IMessageSerializationStrategy messageSerializationStrategy
    )
    {
        this.connection = connection;
        this.channelFactory = channelFactory;
        this.produceConsumeInterceptors = produceConsumeInterceptors.ToArray();
        this.messageSerializationStrategy = messageSerializationStrategy;
    }

    /// <inheritdoc />
    public IPullingConsumer<PullResult> CreateConsumer(in Queue queue, in PullingConsumerOptions options)
    {
        var channel = channelFactory.CreatePersistentChannel(connection, new PersistentChannelOptions());
        return new PullingConsumer(options, queue, channel, produceConsumeInterceptors);
    }

    /// <inheritdoc />
    public IPullingConsumer<PullResult<T>> CreateConsumer<T>(in Queue queue, in PullingConsumerOptions options)
    {
        var consumer = CreateConsumer(queue, options);
        return new PullingConsumer<T>(consumer, messageSerializationStrategy);
    }
}
