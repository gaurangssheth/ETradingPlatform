using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Shared.ConnnectionStringNames;
using TradingApp.Shared.Messaging;
using TradingApp.Shared.Messaging.Correlation;

namespace OrderService.Configuration
{
    public static class NServiceBusConfiguration
    {
        public static EndpointConfiguration ConfigureOrderServiceEndpoint(this HostBuilderContext context)
        {
            var orderDb = context.Configuration.GetConnectionString(ConnectionStringNames.OrderDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.OrderDb}");
            var rabbitMqConnection = context.Configuration["RabbitMQ:Connection"]
            ?? throw new InvalidOperationException("Missing RabbitMQ:Connection");

            var endpointConfiguration = new EndpointConfiguration(EndpointNames.OrderService);

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();

            transport.ConnectionString(rabbitMqConnection);

            transport.UseConventionalRoutingTopology(QueueType.Quorum);

            var routing = transport.Routing();
            routing.RouteToEndpoint(typeof(CaptureTrade), EndpointNames.TradeCaptureService);

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();

            persistence.SqlDialect<SqlDialect.MsSqlServer>();

            persistence.ConnectionBuilder(() =>
                new Microsoft.Data.SqlClient.SqlConnection(orderDb));

            endpointConfiguration.EnableOutbox();
            endpointConfiguration.EnableInstallers();
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.AuditProcessedMessagesTo("audit");

            var recoverability = endpointConfiguration.Recoverability();

            recoverability.Immediate(immediate =>
                immediate.NumberOfRetries(0));

            recoverability.Delayed(delayed =>
            {
                delayed.NumberOfRetries(0);
                delayed.TimeIncrease(TimeSpan.FromSeconds(1));
            });

            endpointConfiguration.Pipeline.Register(
                behavior: new IncomingCorrelationIdBehavior(),
                description: "Adds CorrelationId from incoming NServiceBus headers to Serilog LogContext."
            );

            endpointConfiguration.Pipeline.Register(
                behavior: new OutgoingCorrelationIdBehavior(),
                description: "Adds CorrelationId from outgoing message body to NServiceBus headers."
            );

            return endpointConfiguration;
        }
    }
}
