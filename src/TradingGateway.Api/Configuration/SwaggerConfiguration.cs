using TradingGateway.Api.Swagger;

namespace TradingGateway.Api.Configuration
{
    public static class SwaggerConfiguration
    {
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "Trading Gateway API",
                    Version = "v1",
                    Description = "API for Trading Gateway"
                });
                options.OperationFilter<CorrelationIdHeaderOperationFilter>();
            });
            return services;
        }

        public static IApplicationBuilder UseSwaggerConfiguration(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Trading Gateway API v1");
                options.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
            });

            return app;
        }
    }
}
