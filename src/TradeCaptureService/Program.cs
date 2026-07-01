using Microsoft.EntityFrameworkCore;
using NServiceBus;
using Serilog;
using TradeCaptureService.Configuration;

Console.Title = "ETrading - TradeCaptureService";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("serilog.json", optional: false, reloadOnChange: true);
        config.AddJsonFile(
            $"serilog.{context.HostingEnvironment.EnvironmentName}.json",
            optional: true,
            reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddTradeCaptureDatabase(context.Configuration);
        services.AddPositionApplicationServices(context.Configuration);
    })
    .UseTradeCaptureServiceSerilog()
    .UseNServiceBus(context =>
    {
        return context.ConfigureTradeCaptureEndpoint();
    })
    .Build();

await host.RunAsync();
