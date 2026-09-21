using TradingApp.Shared.Validation;
using TradingGateway.Api.Application.Commands;
using TradingGateway.Api.Application.Commands.SubmitOrder;
using TradingGateway.Api.Application.Commands.SubmitOrder.Validation;
using TradingGateway.Api.Application.Queries;
using TradingGateway.Api.Application.Queries.Positions;
using TradingGateway.Api.Application.Queries.Pricing;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Configuration
{
    public static class ServicesConfiguration
    {
        public static IServiceCollection ConfigureServices(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();

            services.AddScoped<ICommandHandler<SubmitOrderCommand, SubmitOrderResult>, SubmitOrderCommandHandler>();
            services.AddScoped<IQueryHandler<GetOpenPositionsByClientIdQuery, IReadOnlyList<PositionSummaryResponse>>, GetOpenPositionsByClientIdQueryHandler>();
            services.AddScoped<IQueryHandler<GetMarketQuotesQuery, IReadOnlyList<MarketQuoteResponse>>, GetMarketQuotesQueryHandler>();
            services.AddScoped<IQueryDispatcher, QueryDispatcher>();
            services.AddScoped<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<IPolymorphicValidator, SubmitOrderCommandValidator>();
            services.AddScoped<IValidatorFactory, ValidatorFactory>();

            services.AddScoped<IPositionServiceClient, GrpcPositionServiceClient>();
            services.AddScoped<IPricingServiceClient, GrpcPricingServiceClient>();

            var pricingServiceUrl = configuration["PricingService:Url"];
            var positionServiceUrl = configuration["PositionService:Url"];

            if (string.IsNullOrWhiteSpace(pricingServiceUrl))
            {
                throw new InvalidOperationException(
                    "PricingService:Url configuration is missing.");
            }

            if (string.IsNullOrWhiteSpace(positionServiceUrl))
            {
                throw new InvalidOperationException(
                    "PositionService:Url configuration is missing.");
            }

            services.AddGrpcClient<PricingService.Grpc.Pricing.PricingClient>(options =>
            {
                options.Address = new Uri(pricingServiceUrl);
            });

            services.AddGrpcClient<PositionService.Grpc.PositionService.PositionServiceClient>(options =>
            {
                options.Address = new Uri(positionServiceUrl);
            });

            return services;
        }
    }
}
