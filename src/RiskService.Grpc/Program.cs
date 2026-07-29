using RiskService.Grpc.Configuration;
using RiskService.Grpc.Services;

Console.Title = "ETrading - RiskService.Grpc";

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogConfiguration();

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddRiskApplicationServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
app.MapGrpcService<RiskGrpcService>();

app.MapGet("/", () => "RiskService.Grpc is running. Use a gRPC client to call CheckOrderRisk.");

app.Run();
