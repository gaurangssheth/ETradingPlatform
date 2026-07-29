
using ReferenceDataService.Domain.Instruments;
using ReferenceDataService.Grpc.Configuration;
using ReferenceDataService.Grpc.Mapping;
using ReferenceDataService.Grpc.Services;
using ReferenceDataService.Infrastructure.Repositories;

Console.Title = "ETrading - ReferenceDataService.Grpc";

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogConfiguration();

// Add services to the container.
builder.Services.AddGrpc();

builder.Services.AddSingleton<
    IInstrumentRepository,
    InMemoryInstrumentRepository>();
builder.Services.AddSingleton<
    IInstrumentGrpcMapper,
    InstrumentGrpcMapper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<ReferenceDataGrpcService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
