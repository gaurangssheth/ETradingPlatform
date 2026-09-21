using Microsoft.EntityFrameworkCore;
using NServiceBus;
using Serilog;
using TradeCaptureService.Configuration;

Console.Title = "ETrading - TradeCaptureService";
Console.WriteLine("TradeCaptureService is running.");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddSerilogConfiguration(context.HostingEnvironment);
    })
    .ConfigureServices((context, services) =>
    {
        services.ConfigureDatabase(context.Configuration);
        services.ConfigureServices(context.Configuration);
    })
    .UseSerilogConfiguration()
    .UseNServiceBus(context =>
    {
        return context.ConfigureServiceEndpoint();
    })
    .Build();

await host.RunAsync();
