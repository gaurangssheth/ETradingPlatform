using Serilog;
using TradingApp.Shared.Correlation;

namespace TradingGateway.Api.Configuration;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder AddTradingGatewaySerilog(
        this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile(
            "serilog.json",
            optional: false,
            reloadOnChange: true);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    public static IApplicationBuilder UseTradingGatewaySerilog(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var correlationId = httpContext.Items[CorrelationConstants.HeaderName]?.ToString();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    diagnosticContext.Set("CorrelationId", correlationId);
                }
            };
        });
        return app;
    }
}