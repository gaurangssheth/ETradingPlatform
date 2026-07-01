using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.Shared.Messaging.Correlation
{
    public static class GrpcCorrelationConstants
    {
        public const string MetadataKey = "x-correlation-id";
    }
}
