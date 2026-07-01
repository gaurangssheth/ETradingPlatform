using FluentAssertions;
using Moq;
using NServiceBus.Pipeline;
using NServiceBus.Unicast.Messages;
using Serilog;
using Serilog.Core;
using Serilog.Events;
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
    public class IncomingCorrelationIdBehaviorTests
    {
        [Fact]
        public async Task Invoke_WhenHeaderExists_ShouldPushCorrelationIdIntoSerilogContext()
        {
            var headers = new Dictionary<string, string>
            {
                [CorrelationConstants.HeaderName] = "incoming-header-correlation-001"
            };

            var message = new TestCorrelatedCommand
            {
                CorrelationId = "message-correlation"
            };

            var context = CreateIncomingContext(message, headers);

            var sink = new InMemorySink();

            var previousLogger = Log.Logger;

            try
            {
                Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .CreateLogger();

                var behavior = new IncomingCorrelationIdBehavior();

                await behavior.Invoke(context.Object, () =>
                {
                    Log.Information("Inside incoming correlation behaviour");
                    return Task.CompletedTask;
                });

                var logEvent = sink.Events.Should().ContainSingle().Subject;

                logEvent.Properties.Should().ContainKey(CorrelationConstants.LogPropertyName);

                logEvent.Properties[CorrelationConstants.LogPropertyName]
                    .ToString()
                    .Trim('"')
                    .Should()
                    .Be("incoming-header-correlation-001");
            }
            finally
            {
                Log.Logger = previousLogger;
            }
        }

        [Fact]
        public async Task Invoke_WhenHeaderMissing_ShouldFallbackToMessageCorrelationId()
        {
            var headers = new Dictionary<string, string>();

            var message = new TestCorrelatedCommand
            {
                CorrelationId = "message-body-correlation-001"
            };

            var context = CreateIncomingContext(message, headers);

            var sink = new InMemorySink();

            var previousLogger = Log.Logger;

            try
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var behavior = new IncomingCorrelationIdBehavior();

                await behavior.Invoke(context.Object, () =>
                {
                    Log.Information("Inside incoming correlation behaviour");
                    return Task.CompletedTask;
                });

                var logEvent = sink.Events.Should().ContainSingle().Subject;

                logEvent.Properties[CorrelationConstants.LogPropertyName]
                    .ToString()
                    .Trim('"')
                    .Should()
                    .Be("message-body-correlation-001");
            }
            finally
            {
                Log.Logger = previousLogger;
            }
        }

        [Fact]
        public async Task Invoke_WhenNoHeaderAndMessageIsNotCorrelated_ShouldUseNotSet()
        {
            var headers = new Dictionary<string, string>();

            var message = new NonCorrelatedCommand();

            var context = CreateIncomingContext(message, headers);

            var sink = new InMemorySink();

            var previousLogger = Log.Logger;

            try
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var behavior = new IncomingCorrelationIdBehavior();

                await behavior.Invoke(context.Object, () =>
                {
                    Log.Information("Inside incoming correlation behaviour");
                    return Task.CompletedTask;
                });

                var logEvent = sink.Events.Should().ContainSingle().Subject;

                logEvent.Properties[CorrelationConstants.LogPropertyName]
                    .ToString()
                    .Trim('"')
                    .Should()
                    .Be("Not_Set");
            }
            finally
            {
                Log.Logger = previousLogger;
            }
        }

        [Fact]
        public async Task Invoke_WhenHeaderMissingAndIncomingEventHasCorrelationId_ShouldFallbackToMessageCorrelationId()
        {
            var headers = new Dictionary<string, string>();

            var message = new TestCorrelatedEvent
            {
                CorrelationId = "incoming-event-body-correlation-001"
            };

            var context = CreateIncomingContext(message, headers);

            var sink = new InMemorySink();
            var previousLogger = Log.Logger;

            try
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var behavior = new IncomingCorrelationIdBehavior();

                await behavior.Invoke(context.Object, () =>
                {
                    Log.Information("Inside incoming correlation behaviour");
                    return Task.CompletedTask;
                });

                var logEvent = sink.Events.Should().ContainSingle().Subject;

                var correlationProperty = logEvent.Properties[CorrelationConstants.LogPropertyName]
                    .Should()
                    .BeOfType<ScalarValue>()
                    .Subject;

                correlationProperty.Value.Should().Be("incoming-event-body-correlation-001");
            }
            finally
            {
                Log.Logger = previousLogger;
            }
        }

        private static Mock<IIncomingLogicalMessageContext> CreateIncomingContext(
            object message,
            Dictionary<string, string> headers)
        {
            var mockContext = new Mock<IIncomingLogicalMessageContext>();
            mockContext.SetupGet(c => c.Headers).Returns(headers);
            mockContext.SetupGet(c => c.Message).Returns(new LogicalMessage(
                new MessageMetadata(message.GetType()),
                message));
            return mockContext;
        }

        private sealed class InMemorySink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new();

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
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
