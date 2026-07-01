using NServiceBus.Pipeline;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Correlation;

namespace TradingApp.Shared.Messaging.Correlation
{
    public sealed class IncomingCorrelationIdBehavior : 
        Behavior<IIncomingLogicalMessageContext>
    {
        public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
        {
            var correlationId = TryGetCorrelationId(context);

            using (LogContext.PushProperty(CorrelationConstants.LogPropertyName, correlationId))
            {
                return next();
            }
        }

        private static string TryGetCorrelationId(IIncomingLogicalMessageContext context)
        {
            if (context.Headers.TryGetValue(CorrelationConstants.HeaderName, 
                out var correlationIdFromHeader) && !string.IsNullOrWhiteSpace(correlationIdFromHeader))
            {
                return correlationIdFromHeader;
            }

            if (context.Message.Instance is ICorrelatedMessage correlatedMessage
                && !string.IsNullOrWhiteSpace(correlatedMessage.CorrelationId))
            {
                return correlatedMessage.CorrelationId;
            }

            return "Not_Set";
        }
    }
}
