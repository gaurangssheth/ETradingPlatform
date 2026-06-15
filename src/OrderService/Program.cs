using OrderService.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("serilog.json", optional: false, reloadOnChange: true);
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