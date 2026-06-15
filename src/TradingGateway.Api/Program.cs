using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TradingGateway.Api.Middlewares;
using TradingGateway.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddTradingGatewaySerilog();

// Swagger
builder.Services.AddSwaggerConfiguration();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Ui", policy =>
    {
        policy
            .WithOrigins("http://localhost:5174", "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


var gatewayDb = builder.Configuration.GetConnectionString("GatewayDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:GatewayDb");

builder.Services.AddTradingGatewayApplicationServices();

// Redis distributed cache
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration["Redis:Connection"];
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Connection"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});

builder.UseNServiceBus(builder.ConfigureTradingGatewayEndpoint());
builder.Services.AddSignalR();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseTradingGatewaySerilog();
// Enable Swagger
app.UseSwaggerConfiguration();

app.UseCors("Ui");
app.MapControllers();
app.Run();
