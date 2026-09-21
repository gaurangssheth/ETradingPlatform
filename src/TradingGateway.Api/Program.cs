using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TradingGateway.Api.Middlewares;
using TradingGateway.Api.Configuration;

Console.Title = "ETrading - TradingGateway.Api";

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSerilogConfiguration(builder.Environment);
builder.UseSerilogConfiguration();

// Swagger
builder.Services.ConfigureSwagger();
builder.Services.AddCors(options =>
{
    options.AddPolicy("TradingWorkstation", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


var gatewayDb = builder.Configuration.GetConnectionString("GatewayDb")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:GatewayDb");

builder.Services.ConfigureServices(builder.Configuration);

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

builder.UseNServiceBus(builder.ConfigureServiceEndpoint());
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogConfiguration();
// Enable Swagger
app.UseSwaggerConfiguration();

app.UseExceptionHandler();

app.UseCors("TradingWorkstation");

app.MapControllers();
app.Run();
