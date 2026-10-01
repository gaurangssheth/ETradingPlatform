using PricingService.Configuration;
using PricingService.Grpc.Configuration;
using PricingService.Grpc.MarketData;
using PricingService.Grpc.Services;

Console.Title = "ETrading - PricingService.Grpc";
Console.WriteLine("PricingService.Grpc is running.");

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSerilogConfiguration(builder.Environment);
builder.UseSerilogConfiguration();

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.ConfigureServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<PricingGrpcService>();

//app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
app.MapGet("/", () => "PricingService.Grpc is running. Use a gRPC client to call GetPrice.");

app.Run();
