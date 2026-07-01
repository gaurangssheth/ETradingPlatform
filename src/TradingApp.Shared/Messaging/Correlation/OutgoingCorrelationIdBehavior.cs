using NServiceBus.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Correlation;

namespace TradingApp.Shared.Messaging.Correlation
{
    public sealed class OutgoingCorrelationIdBehavior : Behavior<IOutgoingLogicalMessageContext>
    {
        public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
        {
            if (!context.Headers.ContainsKey(CorrelationConstants.HeaderName) 
                && context.Message.Instance is ICorrelatedMessage correlatedMessage
                && !string.IsNullOrWhiteSpace(correlatedMessage.CorrelationId))
            {
                context.Headers[CorrelationConstants.HeaderName] = correlatedMessage.CorrelationId;
            }

            await next();
        }
    }
}
