using Grpc.Core;
using PricingService.Grpc.MarketData;
using Serilog.Context;
using System.Globalization;
using TradingApp.MarketData.Contracts;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace PricingService.Grpc.Services
{
    public class PricingGrpcService : Pricing.PricingBase
    {
        private readonly MarketQuoteCache marketQuoteCache;
        private readonly MarketQuoteStream marketQuoteStream;
        private readonly MarketCandleStore candleStore;
        private readonly MarketCandleStream marketCandleStream;
        private ILogger<PricingGrpcService> logger;

        public PricingGrpcService(
            MarketQuoteCache marketQuoteCache, 
            MarketQuoteStream marketQuoteStream,
            MarketCandleStore candleStore,
            MarketCandleStream marketCandleStream,
            ILogger<PricingGrpcService> logger)
        {
            this.marketQuoteCache = marketQuoteCache;
            this.marketQuoteStream = marketQuoteStream;
            this.candleStore = candleStore;
            this.marketCandleStream = marketCandleStream;
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

        public override Task<GetMarketQuotesResponse> GetMarketQuotes(GetMarketQuotesRequest request, ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                logger.LogInformation("Market quote lookup started. CorrelationId={CorrelationId}",
                    correlationId);

                var response = new GetMarketQuotesResponse();

                foreach (var quote in marketQuoteCache.GetAll().OrderBy(x => x.Symbol))
                {
                    response.Quotes.Add(new MarketQuote
                    {
                        Symbol = quote.Symbol,
                        Bid = quote.Bid.ToString(CultureInfo.InvariantCulture),
                        Ask = quote.Ask.ToString(CultureInfo.InvariantCulture),
                        Timestamp = quote.Timestamp.ToString("O")
                    });
                }

                logger.LogInformation(
                    "Market quote lookup completed. QuoteCount={QuoteCount}, CorrelationId={CorrelationId}",
                    response.Quotes.Count,
                    correlationId);

                return Task.FromResult(response);
            }
        }

        public override async Task StreamMarketQuotes(
            GetMarketQuotesRequest request, 
            IServerStreamWriter<MarketQuote> responseStream, 
            ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                logger.LogInformation("Market quote stream started. CorrelationId={CorrelationId}",
                    correlationId);

                try
                {
                    await foreach(var tick in marketQuoteStream.ReadAllAsync(context.CancellationToken))
                    {
                        var quote = new MarketQuote
                        {
                            Symbol = tick.Symbol,
                            Bid = tick.Bid.ToString(CultureInfo.InvariantCulture),
                            Ask = tick.Ask.ToString(CultureInfo.InvariantCulture),
                            Timestamp = tick.Timestamp.ToString("O")
                        };
                        await responseStream.WriteAsync(quote, context.CancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (context.CancellationToken.IsCancellationRequested)
                {
                    // Expected when the caller closes the stream.
                }
                finally
                {
                    logger.LogInformation("Market quote stream stopped. CorrelationId={CorrelationId}",
                        correlationId);
                }
            }           
        }

        public override Task<GetMarketCandlesResponse> GetMarketCandles(
            GetMarketCandlesRequest request,
            ServerCallContext context)
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
                    throw new RpcException(
                        new Status(
                            StatusCode.InvalidArgument,
                            "Symbol is required."));
                }

                logger.LogInformation(
                    "Market candle lookup started. Symbol={Symbol}, CorrelationId={CorrelationId}",
                    symbol,
                    correlationId);

                var candles = this.candleStore.GetCandles(request.Symbol!).TakeLast(100);

                var response = new GetMarketCandlesResponse();

                foreach (var candle in candles)
                {
                    response.Candles.Add(new MarketCandle
                    {
                        Symbol = candle.Symbol,
                        StartTime = candle.StartTime.ToString("O"),
                        Open = candle.Open.ToString(CultureInfo.InvariantCulture),
                        High = candle.High.ToString(CultureInfo.InvariantCulture),
                        Low = candle.Low.ToString(CultureInfo.InvariantCulture),
                        Close = candle.Close.ToString(CultureInfo.InvariantCulture)
                    });
                }

                logger.LogInformation(
                    "Market candle lookup completed. Symbol={Symbol}, CandleCount={CandleCount}, CorrelationId={CorrelationId}",
                    symbol,
                    response.Candles.Count,
                    correlationId);

                return Task.FromResult(response);
            }            
        }


        public override async Task StreamMarketCandles(
            StreamMarketCandlesRequest request,
            IServerStreamWriter<MarketCandle> responseStream,
            ServerCallContext context)
        {
            var correlationId = context.RequestHeaders.GetValue(GrpcCorrelationConstants.MetadataKey);

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = "Not_Set";
            }

            using (LogContext.PushProperty(GrpcCorrelationConstants.MetadataKey, correlationId))
            {
                logger.LogInformation("Market candle stream started. CorrelationId={CorrelationId}",
                    correlationId);

                try
                {
                    await foreach (var candle in marketCandleStream.ReadAllAsync(context.CancellationToken))
                    {
                        var response = new MarketCandle
                        {
                            Symbol = candle.Symbol,
                            StartTime = candle.StartTime.ToString("O"),
                            Open = candle.Open.ToString(CultureInfo.InvariantCulture),
                            High = candle.High.ToString(CultureInfo.InvariantCulture),
                            Low = candle.Low.ToString(CultureInfo.InvariantCulture),
                            Close = candle.Close.ToString(CultureInfo.InvariantCulture)
                        };
                        await responseStream.WriteAsync(response, context.CancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (context.CancellationToken.IsCancellationRequested)
                {
                    // Expected when the caller closes the stream.
                }
                finally
                {
                    logger.LogInformation("Market candle stream stopped. CorrelationId={CorrelationId}",
                        correlationId);
                }
            }
        }
    }
}
