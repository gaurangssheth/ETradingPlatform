using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.Shared.Messaging
{
    public static class EndpointNames
    {
        public const string TradingGatewayApi = "TradingGateway.Api";

        public const string OrderService = "Order.Service";

        public const string TradeCaptureService = "TradeCapture.Service";

        public const string PositionService = "Position.Service";
    }
}
