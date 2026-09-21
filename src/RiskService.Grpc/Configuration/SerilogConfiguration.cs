using Serilog;

namespace RiskService.Grpc.Configuration;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder UseSerilogConfiguration(
        this WebApplicationBuilder builder)
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
}