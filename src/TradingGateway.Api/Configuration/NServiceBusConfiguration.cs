using Contracts;
using NServiceBus.TransactionalSession;
using TradingApp.Contracts.Commands;
using TradingApp.Shared.Messaging;

namespace TradingGateway.Api.Configuration
{
    public static class NServiceBusConfiguration
    {
        public static EndpointConfiguration ConfigureTradingGatewayEndpoint(
        this WebApplicationBuilder builder)
        {
            var gatewayDb = builder.Configuration.GetConnectionString("GatewayDb")
               ?? throw new InvalidOperationException("Missing ConnectionStrings:GatewayDb");

            var rabbitMqConnection = builder.Configuration["RabbitMQ:Connection"]
            ?? throw new InvalidOperationException("Missing RabbitMQ:Connection");

            var endpointConfiguration = new EndpointConfiguration(EndpointNames.TradingGatewayApi);
            endpointConfiguration.UseSerialization<SystemJsonSerializer>();

            var transport = endpointConfiguration.UseTransport<RabbitMQTransport>();

            transport.ConnectionString(rabbitMqConnection);
            transport.UseConventionalRoutingTopology(QueueType.Quorum);

            var routing = transport.Routing();

            routing.RouteToEndpoint(typeof(SubmitOrder), EndpointNames.OrderService);

            var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
            persistence.SqlDialect<SqlDialect.MsSqlServer>();
            persistence.ConnectionBuilder(() => new Microsoft.Data.SqlClient.SqlConnection(gatewayDb));
            persistence.EnableTransactionalSession();
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
