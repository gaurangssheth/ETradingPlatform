using OrderService.Application.Commands;
using OrderService.Application.Queries;
using TradingApp.Shared.Validation;
using TradingGateway.Api.Application.Commands.SubmitOrder;
using TradingGateway.Api.Validation;

namespace TradingGateway.Api.Configuration
{
    public static class ApplicationServicesConfiguration
    {
        public static IServiceCollection AddTradingGatewayApplicationServices(
            this IServiceCollection services)
        {
            services.AddControllers();

            services.AddScoped<ICommandHandler<SubmitOrderCommand, SubmitOrderResult>, SubmitOrderCommandHandler>();
            services.AddScoped<IQueryDispatcher, QueryDispatcher>();
            services.AddScoped<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<IPolymorphicValidator, SubmitOrderCommandValidator>();
            services.AddScoped<IValidatorFactory, ValidatorFactory>();

            return services;
        }
    }
}
