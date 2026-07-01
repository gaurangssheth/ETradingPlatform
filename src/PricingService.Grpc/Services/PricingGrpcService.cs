using Grpc.Core;
using Serilog.Context;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace PricingService.Grpc.Services
{
    public class PricingGrpcService : Pricing.PricingBase
    {
        private ILogger<PricingGrpcService> logger;

        private static readonly IReadOnlyDictionary<string, double> MidPrices = new Dictionary<string, double>
        {
            ["EURUSD"] = 1.0850,
            ["GBPUSD"] = 1.2700,
            ["USDJPY"] = 157.50
        };

        private static readonly IReadOnlyDictionary<string, double> Spreads = new Dictionary<string, double>
        {
            ["EURUSD"] = 0.0002,
            ["GBPUSD"] = 0.0003,
            ["USDJPY"] = 0.02
        };

        public PricingGrpcService(ILogger<PricingGrpcService> logger)
        {
            this.logger = logger;
        }

        public override Task<GetPriceResponse> GetPrice(GetPriceRequest request, ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                var symbol = request.Symbol?.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(symbol))
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument,
                        "Symbol is required."
                        ));
                }

                if (!MidPrices.TryGetValue(symbol, out var mid))
                {
                    throw new RpcException(new Status(
                        StatusCode.NotFound,
                        $"No price configured for symbol {symbol}."
                    ));
                }

                if (!Spreads.TryGetValue(symbol, out var spread))
                {
                    throw new RpcException(new Status(
                        StatusCode.NotFound,
                        $"No spread configured for symbol {symbol}."
                    ));
                }

                //var mid = symbol switch
                //{
                //    "EURUSD" => 1.0850,
                //    "GBPUSD" => 1.2700,
                //    "USDJPY" => 157.50,
                //    _ => throw new RpcException(new Status(
                //        StatusCode.NotFound,
                //        $"No price configured for {symbol}."
                //        ))

                //};

                //var spread = symbol == "USDJPY" ? 0.02 : 0.0002;
                var bid = mid - spread / 2;
                var ask = mid + spread / 2;

                var response = new GetPriceResponse
                {
                    Symbol = symbol,
                    Bid = bid,
                    Ask = ask,
                    Mid = mid
                };

                logger.LogInformation(
                    "Price returned. CorrelationId={CorrelationId}, Symbol={Symbol}, Bid={Bid}, Ask={Ask}, Mid={Mid}",
                    correlationId ?? "Not_Set",
                    response.Symbol,
                    response.Bid,
                    response.Ask,
                    response.Mid);

                return Task.FromResult(response);
            }
        }
    }
}
