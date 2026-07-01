using FluentAssertions;
using Moq;
using NServiceBus.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace TradingApp.Shared.Tests
{
    public class OutgoingCorrelationIdBehaviorTests
    {
        [Fact]
        public async Task Invoke_WhenOutgoingCommandImplementsICorrelatedMessage_ShouldSetCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>();

            var mesage = new TestCorrelatedCommand
            {
                CorrelationId = "outgoing-correlation-001"
            };

            var context = CreateOutgoingContext(mesage, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            var nextCalled = false;

            await behavior.Invoke(context.Object, () =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            nextCalled.Should().BeTrue();

            headers.Should().HaveCount(1);
            headers.Should().ContainKey(CorrelationConstants.HeaderName);
            headers[CorrelationConstants.HeaderName].Should().Be("outgoing-correlation-001");
        }

        [Fact]
        public async Task Invoke_WhenHeaderAlreadyExists_ShouldNotOverwriteCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>
            {
                [CorrelationConstants.HeaderName] = "existing-header-correlation"
            };

            var message = new TestCorrelatedCommand
            {
                CorrelationId = "message-correlation"
            };

            var context = CreateOutgoingContext(message, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            await behavior.Invoke(context.Object, () => Task.CompletedTask);

            headers[CorrelationConstants.HeaderName].Should().Be("existing-header-correlation");
        }

        [Fact]
        public async Task Invoke_WhenMessageDoesNotImplementICorrelatedMessage_ShouldNotSetCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>();

            var message = new NonCorrelatedCommand();

            var context = CreateOutgoingContext(message, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            await behavior.Invoke(context.Object, () => Task.CompletedTask);

            headers.Should().NotContainKey(CorrelationConstants.HeaderName);
        }

        [Fact]
        public async Task Invoke_WhenCorrelationIdIsEmpty_ShouldNotSetCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>();

            var message = new TestCorrelatedCommand
            {
                CorrelationId = ""
            };

            var context = CreateOutgoingContext(message, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            await behavior.Invoke(context.Object, () => Task.CompletedTask);

            headers.Should().NotContainKey(CorrelationConstants.HeaderName);
        }

        [Fact]
        public async Task Invoke_WhenOutgoingEventImplementsICorrelatedMessage_ShouldSetCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>();

            var message = new TestCorrelatedEvent
            {
                CorrelationId = "outgoing-event-correlation-001"
            };

            var context = CreateOutgoingContext(message, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            await behavior.Invoke(context.Object, () => Task.CompletedTask);

            headers.Should().ContainKey(CorrelationConstants.HeaderName);
            headers[CorrelationConstants.HeaderName].Should().Be("outgoing-event-correlation-001");
        }

        [Fact]
        public async Task Invoke_WhenOutgoingEventDoesNotImplementICorrelatedMessage_ShouldNotSetCorrelationIdHeader()
        {
            var headers = new Dictionary<string, string>();

            var message = new NonCorrelatedEvent();

            var context = CreateOutgoingContext(message, headers);

            var behavior = new OutgoingCorrelationIdBehavior();

            await behavior.Invoke(context.Object, () => Task.CompletedTask);

            headers.Should().NotContainKey(CorrelationConstants.HeaderName);
        }

        private static Mock<IOutgoingLogicalMessageContext> CreateOutgoingContext(
            object message,
            Dictionary<string, string> headers)
        {
            var mockContext = new Mock<IOutgoingLogicalMessageContext>();
            mockContext.SetupGet(c => c.Headers).Returns(headers);
            mockContext.SetupGet(c => c.Message).Returns(new OutgoingLogicalMessage(
                message.GetType(), 
                message));
            return mockContext;
        }

        private sealed class TestCorrelatedCommand : ICommand, ICorrelatedMessage
        {
            public string CorrelationId { get; set; } = null!;
        }

        private sealed class TestCorrelatedEvent : IEvent, ICorrelatedMessage
        {
            public string CorrelationId { get; set; } = null!;
        }

        private sealed class NonCorrelatedCommand : ICommand
        {
        }

        private sealed class NonCorrelatedEvent : IEvent
        {
        }
    }
}
