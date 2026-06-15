using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Shared.ConnnectionStringNames;
using TradingApp.Shared.Messaging;

namespace PositionService.Configuration
{
    public static class NServiceBusConfiguration
    {
        public static EndpointConfiguration ConfigurePoisitionServiceEndpoint(this HostBuilderContext context)
        {
            var positionDb = context.Configuration.GetConnectionString(ConnectionStringNames.PositionDb)
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{ConnectionStringNames.PositionDb}");
            var rabbitMqConnection = context.Configuration["RabbitMQ:Connection"]
            ?? throw new InvalidOperationException("Missing RabbitMQ:Connection");

            var endpointConfiguration = new EndpointConfiguration(EndpointNames.PositionService);

            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();

            transport.ConnectionString(context.Configuration["RabbitMQ:Connection"]
                ?? throw new InvalidOperationException("Missing RabbitMQ:Connection"));

            transport.UseConventionalRoutingTopology(QueueType.Quorum);

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();

            persistence.SqlDialect<SqlDialect.MsSqlServer>();

            persistence.ConnectionBuilder(() =>
                new Microsoft.Data.SqlClient.SqlConnection(positionDb));

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

            return endpointConfiguration;
        }
    }
}
