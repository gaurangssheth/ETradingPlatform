using Serilog;
using TradingApp.Shared.Correlation;

namespace TradingGateway.Api.Configuration;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder UseSerilogConfiguration(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    public static IConfigurationBuilder AddSerilogConfiguration(this IConfigurationBuilder configuration,
        IHostEnvironment environment)
    {
        configuration
            .AddJsonFile("serilog.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"serilog.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

        return configuration;
    }

    public static IApplicationBuilder UseSerilogConfiguration(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var correlationId = httpContext.Items[CorrelationConstants.HeaderName]?.ToString();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    diagnosticContext.Set(CorrelationConstants.LogPropertyName, correlationId);
                }
            };
        });
        return app;
    }
}