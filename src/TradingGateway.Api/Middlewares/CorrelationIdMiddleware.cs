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
            if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId))
            {
                correlationId = Guid.NewGuid().ToString("N");
                context.Request.Headers[HeaderName] = correlationId;
            }

            context.Items[HeaderName] = correlationId.ToString();

            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await next(context);
            }
        }
    }
}
