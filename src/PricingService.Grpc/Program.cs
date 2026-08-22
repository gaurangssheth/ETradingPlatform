using PricingService.Grpc.Configuration;
using PricingService.Grpc.MarketData;
using PricingService.Grpc.Services;

Console.Title = "ETrading - PricingService.Grpc";

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogConfiguration();

// Add services to the container.
builder.Services.AddGrpc();

var marketDataEndpoint = builder.Configuration.GetValue<string>("MarketData:Endpoint") ??
    throw new InvalidOperationException("Marketdata:Endpoint is not configured.");

builder.Services.AddSingleton<MarketQuoteCache>();
builder.Services.AddSingleton<PriceTickSubscriberWorker>(
    serviceProvider => new PriceTickSubscriberWorker(
        serviceProvider.GetRequiredService<MarketQuoteCache>(),
        marketDataEndpoint));

builder.Services.AddHostedService<PriceTickSubscriberHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<PricingGrpcService>();

//app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
app.MapGet("/", () => "PricingService.Grpc is running. Use a gRPC client to call GetPrice.");

app.Run();
