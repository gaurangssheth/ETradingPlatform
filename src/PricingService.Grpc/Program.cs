using PricingService.Grpc.Configuration;
using PricingService.Grpc.Services;

Console.Title = "ETrading - PricingService.Grpc";

var builder = WebApplication.CreateBuilder(args);

builder.AddPricingServiceSerilog();

// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<PricingGrpcService>();

//app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
app.MapGet("/", () => "PricingService.Grpc is running. Use a gRPC client to call GetPrice.");

app.Run();
