using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using PricingService.Grpc;
using PricingService.Grpc.MarketData;
using PricingService.Grpc.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.MarketData.Contracts;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace PricingService.Tests
{
    public class PricingGrpcServiceTests
    {
        [Fact]
        public async Task GetPrice_Should_Return_EurUsd_Price()
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(new PriceTick
            (
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow
            ));


            var service = new PricingGrpcService(marketQuoteCache, NullLogger<PricingGrpcService>.Instance);

            var response = await service.GetPrice(new Grpc.GetPriceRequest
            {
                Symbol = "EURUSD"
            }, TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().BeApproximately(1.0850, 0.0000001);
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
            response.Should().BeOfType<Grpc.GetPriceResponse>();
        }

        [Fact]
        public async Task GetPrice_Should_Normalise_Symbol()
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(
                new PriceTick(
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow));

            var service = new PricingGrpcService(
                marketQuoteCache,
                NullLogger<PricingGrpcService>.Instance);

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = " eurusd " },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().Be(1.0850);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_MarketData_Is_Not_Available()
        {
            var marketQuoteCache = new MarketQuoteCache();

            var service = new PricingGrpcService(
                marketQuoteCache,
                NullLogger<PricingGrpcService>.Instance);

            Func<Task> action = async () => await service.GetPrice(
                new GetPriceRequest { Symbol = "ABCXYZ" },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.Unavailable);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_Symbol_Is_Empty()
        {
            var marketQuoteCache = new MarketQuoteCache();
            var service = new PricingGrpcService(
                marketQuoteCache,
                NullLogger<PricingGrpcService>.Instance);

            var action = async () => await service.GetPrice(
                    new GetPriceRequest { Symbol = "" },
                    TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        }

        [Fact]
        public async Task GetPrice_WhenCorrelationIdHeaderExists_ShouldStillReturnPrice()
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(
                new PriceTick(
                "EURUSD",
                1.0849m,
                1.0851m,
                DateTimeOffset.UtcNow));

            var service = new PricingGrpcService(
                marketQuoteCache,
                NullLogger<PricingGrpcService>.Instance);

            var headers = new Metadata
            {
                { GrpcCorrelationConstants.MetadataKey, "pricing-service-test-001" }
            };

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = "EURUSD" },
                TestServerCallContext.Create(headers));

            response.Symbol.Should().Be("EURUSD");
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
        }

        [Theory]
        [InlineData("AAPL", 210.00, 210.50, 210.25)]
        [InlineData("GB00TEST1234", 98.40, 98.50, 98.45)]
        public async Task GetPrice_WhenMultiAssetSymbolConfigured_ShouldReturnExpectedQuote(
                        string symbol,
                        double expectedBid,
                        double expectedAsk,
                        double expectedMid)
        {
            var marketQuoteCache = new MarketQuoteCache();

            marketQuoteCache.Update(
                new PriceTick(
                symbol,
                (decimal)expectedBid,
                (decimal)expectedAsk,
                DateTimeOffset.UtcNow));

            var service = new PricingGrpcService(
                marketQuoteCache,
                NullLogger<PricingGrpcService>.Instance);

            var response = await service.GetPrice(
                new GetPriceRequest
                {
                    Symbol = symbol
                },
                TestServerCallContext.Create());

            response.Symbol.Should().Be(symbol);

            response.Bid.Should()
                .BeApproximately(expectedBid, 0.0000001);

            response.Ask.Should()
                .BeApproximately(expectedAsk, 0.0000001);

            response.Mid.Should()
                .BeApproximately(expectedMid, 0.0000001);
        }
    }
}
