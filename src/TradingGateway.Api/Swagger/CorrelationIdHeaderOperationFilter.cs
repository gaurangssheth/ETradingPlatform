using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TradingApp.Shared.Correlation;

namespace TradingGateway.Api.Swagger;

public sealed class CorrelationIdHeaderOperationFilter : IOperationFilter
{
    private const string HeaderName = CorrelationConstants.HeaderName;

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        if (operation.Parameters.Any(p => p.Name == HeaderName))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional correlation id used to trace requests across services.",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Example = new Microsoft.OpenApi.Any.OpenApiString("test-order-001")
            }
        });
    }
}