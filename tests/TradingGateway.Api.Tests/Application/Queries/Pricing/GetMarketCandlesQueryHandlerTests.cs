using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingGateway.Api.Application.Queries.Pricing;
using TradingGateway.Api.Application.Queries.Pricing.TradingGateway.Api.Application.Queries.Pricing;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Tests.Application.Queries.Pricing
{
    public class GetMarketCandlesQueryHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ReturnsCandlesFromPricingServiceClient()
        {
            var expected = new List<MarketCandleResponse>
            {
                new(
                    Symbol: "EURUSD",
                    StartTime: new DateTimeOffset(2026, 9, 26, 8, 30, 0, TimeSpan.Zero),
                    Open: 1.0850m,
                    High: 1.0860m,
                    Low: 1.0840m,
                    Close: 1.0855m)
            };

            var pricingServiceClient =
                new Mock<IPricingServiceClient>();

            pricingServiceClient
                .Setup(x => x.GetMarketCandlesAsync(
                    "EURUSD",
                    "correlation-123",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var handler =
                new GetMarketCandlesQueryHandler(
                    pricingServiceClient.Object);

            var result = await handler.HandleAsync(
                new GetMarketCandlesQuery(
                    "EURUSD",
                    "correlation-123"),
                CancellationToken.None);

            result.Should().BeEquivalentTo(expected);

            pricingServiceClient.Verify(
                x => x.GetMarketCandlesAsync(
                    "EURUSD",
                    "correlation-123",
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
