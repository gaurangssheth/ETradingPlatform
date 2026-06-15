using PositionService.Configuration;

Console.WriteLine("PositionService starting...");
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("serilog.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddPositionDatabase(context.Configuration);
    })
    .UsePositionServiceSerilog()
    .UseNServiceBus(context =>
    {
        return context.ConfigurePoisitionServiceEndpoint();
    })
    .Build();

await host.RunAsync();
