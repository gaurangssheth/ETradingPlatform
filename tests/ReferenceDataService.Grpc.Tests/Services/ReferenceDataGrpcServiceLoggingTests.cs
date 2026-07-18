using FluentAssertions;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReferenceDataService.Grpc.Mapping;
using ReferenceDataService.Grpc.Services;
using ReferenceDataService.Infrastructure.Repositories;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace ReferenceDataService.Grpc.Tests.Services
{
    public class ReferenceDataGrpcServiceLoggingTests
    {
        [Fact]
        public async Task GetInstrument_WhenCorrelationIdProvided_ShouldAddCorrelationIdToLogs()
        {
            var sink = new InMemorySink();

            using var serilogLogger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .CreateLogger();

            using var loggerFactory = new SerilogLoggerFactory(
                serilogLogger,
                dispose: false);

            var logger = loggerFactory.CreateLogger<ReferenceDataGrpcService>();

            var service = new ReferenceDataGrpcService(
                new InMemoryInstrumentRepository(),
                new InstrumentGrpcMapper(),
                logger);

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "reference-data-test-001"
                }
            };

            await service.GetInstrument(
                new GetInstrumentRequest
                {
                    Symbol = "EURUSD"
                },
                TestServerCallContext.Create(headers));

            sink.Events.Should().NotBeEmpty();

            sink.Events.Any(logEvent =>
            {
                return logEvent.Properties.TryGetValue(
                    GrpcCorrelationConstants.MetadataKey,
                    out var value)
                    &&
                    value.ToString().Contains("reference-data-test-001");
            }).Should().BeTrue();
        }

        private sealed class InMemorySink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new();

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }
    }
}
