using OrderService.Configuration;

Console.Title = "ETrading - OrderService";

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
        services.AddOrderDatabase(context.Configuration);
    })
    .UseOrderServiceSerilog()
    .UseNServiceBus(context =>
    {
        return context.ConfigureOrderServiceEndpoint();
    })
    .Build();

await host.RunAsync();