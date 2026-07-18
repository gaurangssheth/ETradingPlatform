using Serilog;

namespace ReferenceDataService.Grpc.Configuration;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder AddReferenceDataServiceSerilog(
        this WebApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile(
            "serilog.json",
            optional: false,
            reloadOnChange: true);

        builder.Configuration.AddJsonFile(
            $"serilog.{builder.Environment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: true);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }
}