using OrderService.Configuration;

Console.Title = "ETrading - OrderService";
Console.WriteLine("OrderService is running.");

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