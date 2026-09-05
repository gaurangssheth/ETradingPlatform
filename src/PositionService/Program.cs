using PositionService.Configuration;

Console.Title = "ETrading - PositionService";
Console.WriteLine("PositionService is running.");

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
        services.AddApplicationServices(context.Configuration);
    })
    .UseSerilogConfiguration()
    .UseNServiceBus(context =>
    {
        return context.ConfigurePoisitionServiceEndpoint();
    })
    .Build();

await host.RunAsync();
