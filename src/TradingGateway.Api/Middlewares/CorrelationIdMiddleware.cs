using Microsoft.Extensions.Primitives;
using Serilog.Context;
using TradingApp.Shared.Correlation;

namespace TradingGateway.Api.Middlewares
{
    public sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = CorrelationConstants.HeaderName;

        private readonly RequestDelegate next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);

            context.Items[HeaderName] = correlationId;

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });
            using (LogContext.PushProperty(CorrelationConstants.LogPropertyName, correlationId))
            {
                await next(context);
            }
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(
                    HeaderName,
                    out StringValues correlationIdValues))
            {
                var correlationId = correlationIdValues.FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    return correlationId;
                }
            }

            var newCorrelationId = Guid.NewGuid().ToString("N");

            context.Request.Headers[HeaderName] = newCorrelationId;

            return newCorrelationId;
        }
    }
}

