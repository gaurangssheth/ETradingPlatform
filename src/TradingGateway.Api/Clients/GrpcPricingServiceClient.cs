using Grpc.Core;
using PricingService.Grpc;
using System.Globalization;
using System.Runtime.CompilerServices;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Clients
{
    public class GrpcPricingServiceClient : IPricingServiceClient
    {
        private readonly PricingService.Grpc.Pricing.PricingClient pricingClient;
        private readonly ILogger<GrpcPricingServiceClient> logger;


        public GrpcPricingServiceClient(PricingService.Grpc.Pricing.PricingClient pricingClient,
            ILogger<GrpcPricingServiceClient> logger)
        {
            this.pricingClient = pricingClient;
            this.logger = logger;
        }

        public async Task<IReadOnlyList<MarketQuoteResponse>> GetMarketQuotesAsync(string? correlationId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var headers = new Metadata();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    headers.Add(GrpcCorrelationConstants.MetadataKey, correlationId);
                }

                var response = await pricingClient.GetMarketQuotesAsync(
                    new GetMarketQuotesRequest(),
                    headers,
                    cancellationToken: cancellationToken);

                return response.Quotes.Select(x => new MarketQuoteResponse
                {
                    Symbol = x.Symbol,
                    Bid = Decimal.Parse(x.Bid, CultureInfo.InvariantCulture),
                    Ask = Decimal.Parse(x.Ask, CultureInfo.InvariantCulture),
                    Timestamp = DateTimeOffset.Parse(x.Timestamp, CultureInfo.InvariantCulture)
                }).ToList();
            }
            catch (RpcException ex)
            {
                logger.LogError(ex,
                    "Failed to retrieve market quotes from PricingService.");

                throw;
            }

        }

        public async IAsyncEnumerable<MarketQuoteResponse> StreamMarketQuotesAsync(
            string? correlationId = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var headers = new Metadata();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                headers.Add(
                    GrpcCorrelationConstants.MetadataKey,
                    correlationId);
            }

            using var call = pricingClient.StreamMarketQuotes(
                    new GetMarketQuotesRequest(),
                    headers,
                    cancellationToken: cancellationToken);

            await foreach(var quote in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                yield return new MarketQuoteResponse
                {
                    Symbol = quote.Symbol,
                    Bid = Decimal.Parse(quote.Bid, CultureInfo.InvariantCulture),
                    Ask = Decimal.Parse(quote.Ask, CultureInfo.InvariantCulture),
                    Timestamp = DateTimeOffset.Parse(quote.Timestamp, CultureInfo.InvariantCulture)
                };
            }
        }

        public async Task<IReadOnlyList<MarketCandleResponse>> GetMarketCandlesAsync(
            string symbol,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var headers = new Metadata();

                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    headers.Add(
                        GrpcCorrelationConstants.MetadataKey,
                        correlationId);
                }

                var response = await this.pricingClient.GetMarketCandlesAsync(
                    new GetMarketCandlesRequest
                    {
                        Symbol = symbol
                    },
                    headers,
                    cancellationToken: cancellationToken);

                return response.Candles
                    .Select(candle => new MarketCandleResponse(
                        candle.Symbol,
                        DateTimeOffset.Parse(
                            candle.StartTime,
                            CultureInfo.InvariantCulture),
                        decimal.Parse(
                            candle.Open,
                            CultureInfo.InvariantCulture),
                        decimal.Parse(
                            candle.High,
                            CultureInfo.InvariantCulture),
                        decimal.Parse(
                            candle.Low,
                            CultureInfo.InvariantCulture),
                        decimal.Parse(
                            candle.Close,
                            CultureInfo.InvariantCulture)))
                    .ToList();
            }
            catch (RpcException ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to retrieve market candles from PricingService. Symbol={Symbol}, StatusCode={StatusCode}, CorrelationId={CorrelationId}",
                    symbol,
                    ex.StatusCode,
                    correlationId ?? "Not_Set");

                throw;
            }
        }

        public async IAsyncEnumerable<MarketCandleResponse> StreamMarketCandlesAsync(
            string? correlationId = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var headers = new Metadata();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                headers.Add(
                    GrpcCorrelationConstants.MetadataKey,
                    correlationId);
            }

            var call = pricingClient.StreamMarketCandles(
                new StreamMarketCandlesRequest(),
                headers,
                cancellationToken: cancellationToken);

            await foreach(var candle in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                yield return new MarketCandleResponse(
                    Symbol: candle.Symbol,
                    StartTime: DateTimeOffset.Parse(
                        candle.StartTime,
                        CultureInfo.InvariantCulture),
                    Open: decimal.Parse(
                        candle.Open,
                        CultureInfo.InvariantCulture),
                    High: decimal.Parse(
                        candle.High,
                        CultureInfo.InvariantCulture),
                    Low: decimal.Parse(
                        candle.Low,
                        CultureInfo.InvariantCulture),
                    Close: decimal.Parse(
                        candle.Close,
                        CultureInfo.InvariantCulture)
                );

            }
        }
    }
}
