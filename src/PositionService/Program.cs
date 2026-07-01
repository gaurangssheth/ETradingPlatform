using PositionService.Configuration;

Console.Title = "ETrading - PositionService";

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
        services.AddPositionDatabase(context.Configuration);
        services.AddPositionApplicationServices();
    })
    .UsePositionServiceSerilog()
    .UseNServiceBus(context =>
    {
        return context.ConfigurePoisitionServiceEndpoint();
    })
    .Build();

await host.RunAsync();
