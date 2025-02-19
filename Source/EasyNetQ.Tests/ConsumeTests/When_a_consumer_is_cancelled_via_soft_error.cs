// ReSharper disable InconsistentNaming

using System;
using System.Threading.Tasks;
using EasyNetQ.Persistent;
using EasyNetQ.Tests.Mocking;
using EasyNetQ.Topology;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace EasyNetQ.Tests.ConsumeTests;

public class When_a_consumer_is_cancelled_via_soft_error : IDisposable
{
    private readonly MockBuilder mockBuilder;

    public When_a_consumer_is_cancelled_via_soft_error()
    {
        mockBuilder = new MockBuilder();

        var queue = new Queue("my_queue", false);

        mockBuilder.Bus.Advanced.Consume(
            queue,
            (_, _, _) => Task.Run(() => { }),
            c => c.WithConsumerTag("consumer_tag")
        );

        mockBuilder.Consumers[0].Model.CloseReason.Returns(
            new ShutdownEventArgs(ShutdownInitiator.Application, AmqpErrorCodes.PreconditionFailed, "Oops")
        );
        mockBuilder.Consumers[0].HandleBasicCancel("consumer_tag").GetAwaiter().GetResult();
        // Wait for a periodic consumer restart
        Task.Delay(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        mockBuilder.Dispose();
    }

    [Fact]
    public void Should_recreate_model_and_consumer()
    {
        mockBuilder.Consumers[0].Model.Received().Dispose();
        mockBuilder.Consumers[1].Model.DidNotReceive().Dispose();
    }
}

// ReSharper restore InconsistentNaming
