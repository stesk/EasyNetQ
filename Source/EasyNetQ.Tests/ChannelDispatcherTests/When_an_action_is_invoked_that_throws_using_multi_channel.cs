// ReSharper disable InconsistentNaming

using System;
using System.Threading.Tasks;
using EasyNetQ.ChannelDispatcher;
using EasyNetQ.Consumer;
using EasyNetQ.Persistent;
using EasyNetQ.Producer;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace EasyNetQ.Tests.ChannelDispatcherTests;

public class When_an_action_is_invoked_that_throws_using_multi_channel : IDisposable
{
    private readonly IPersistentChannelDispatcher dispatcher;

    public When_an_action_is_invoked_that_throws_using_multi_channel()
    {
        var channelFactory = Substitute.For<IPersistentChannelFactory>();
        var producerConnection = Substitute.For<IProducerConnection>();
        var consumerConnection = Substitute.For<IConsumerConnection>();
        var channel = Substitute.For<IPersistentChannel>();
        var model = Substitute.For<IModel>();

        channelFactory.CreatePersistentChannel(producerConnection, new PersistentChannelOptions()).Returns(channel);
        channel.InvokeChannelActionAsync<int, FuncBasedPersistentChannelAction<int>>(default)
            .ReturnsForAnyArgs(x => ((FuncBasedPersistentChannelAction<int>)x[0]).Invoke(model));

        dispatcher = new MultiPersistentChannelDispatcher(1, producerConnection, consumerConnection, channelFactory);
    }

    public void Dispose()
    {
        dispatcher.Dispose();
    }

    [Fact]
    public async Task Should_raise_the_exception_on_the_calling_thread()
    {
        await Assert.ThrowsAsync<CrazyTestOnlyException>(
            () => dispatcher.InvokeAsync<int>(_ => throw new CrazyTestOnlyException(), PersistentChannelDispatchOptions.ProducerTopology)
        );
    }

    [Fact]
    public async Task Should_call_action_when_previous_threw_an_exception()
    {
        await Assert.ThrowsAsync<Exception>(
            () => dispatcher.InvokeAsync<int>(_ => throw new Exception(), PersistentChannelDispatchOptions.ProducerTopology)
        );

        var result = await dispatcher.InvokeAsync(_ => 42, PersistentChannelDispatchOptions.ProducerTopology);
        result.Should().Be(42);
    }

    private class CrazyTestOnlyException : Exception { }
}

// ReSharper restore InconsistentNaming
