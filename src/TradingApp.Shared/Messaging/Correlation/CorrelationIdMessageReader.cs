using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Correlation;

namespace TradingApp.Shared.Messaging.Correlation
{
    internal sealed class CorrelationIdMessageReader
    {
        public static string? TryGetCorrelationId(object? message)
        {
            if (message is null)
            {
                return null;
            }

            var property = message.GetType().GetProperty(CorrelationConstants.LogPropertyName, 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var value = property?.GetValue(message) as string;

            return value;
        }
    }
}
