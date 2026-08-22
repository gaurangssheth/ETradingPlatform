using Grpc.Core;
using PricingService.Grpc.MarketData;
using Serilog.Context;
using TradingApp.MarketData.Contracts;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace PricingService.Grpc.Services
{
    public class PricingGrpcService : Pricing.PricingBase
    {
        private readonly MarketQuoteCache marketQuoteCache;
        private ILogger<PricingGrpcService> logger;

        public PricingGrpcService(MarketQuoteCache marketQuoteCache, ILogger<PricingGrpcService> logger)
        {
            this.marketQuoteCache = marketQuoteCache;
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

                if (!this.marketQuoteCache.TryGet(symbol, out var priceTick))
                {
                    throw new RpcException(new Status(
                        StatusCode.Unavailable,
                        $"No market data available for symbol {symbol}."
                    ));
                }

                var bid = (double)priceTick.Bid;
                var ask = (double)priceTick.Ask;
                var mid = (bid + ask) / 2;

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
