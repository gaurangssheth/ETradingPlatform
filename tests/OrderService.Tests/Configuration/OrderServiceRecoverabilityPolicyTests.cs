using FluentAssertions;
using Grpc.Core;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using OrderService.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Tests.Configuration
{
    public class OrderServiceRecoverabilityPolicyTests
    {
        [Fact]
        public void Invoke_WhenGrpcStatusIsInvalidArgument_ShouldMoveMessageToErrorQueue()
        {
            var config = CreateRecoverabilityConfig();

            var context = CreateErrorContext(
                new RpcException(
                    new Status(
                        StatusCode.InvalidArgument,
                        "Invalid argument error occurred."
                    ))
                );

            var action = OrderServiceRecoverabilityPolicy.Invoke(config, context);

            action.Should().BeOfType<MoveToError>();

            var moveToError = (MoveToError)action;

            moveToError.ErrorQueue.Should().Be("error");

        }

        private static RecoverabilityConfig CreateRecoverabilityConfig()
        {
            return new RecoverabilityConfig(
                new ImmediateConfig(maxNumberOfRetries: 2),
                new DelayedConfig(
                    maxNumberOfRetries: 3,
                    timeIncrease: TimeSpan.FromSeconds(5)),
                new FailedConfig(
                    errorQueue: "error",
                    unrecoverableExceptionTypes: [])
            );
        }

        private static ErrorContext CreateErrorContext(Exception exception)
        {
            return new ErrorContext(
                exception,
                headers: [],
                nativeMessageId: "test-message-id",
                body: ReadOnlyMemory<byte>.Empty,
                transportTransaction: new TransportTransaction(),
                immediateProcessingFailures: 1,
                receiveAddress: "OrderService",
                context: new ContextBag()
            );
        }
    }
}