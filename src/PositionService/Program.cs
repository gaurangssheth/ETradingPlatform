using PositionService.Configuration;
using PositionService.Services;

Console.Title = "ETrading - PositionService";
Console.WriteLine("PositionService is running.");

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSerilogConfiguration(builder.Environment);

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.ConfigureDatabase(builder.Configuration);
builder.Services.ConfigureServices(builder.Configuration);

builder.UseSerilogConfiguration();
builder.UseNServiceBus(builder.ConfigureServiceEndpoint());

var app = builder.Build();

// expose PositionService gRPC endpoint.
app.MapGrpcService<PositionGrpcService>();

await app.RunAsync();
