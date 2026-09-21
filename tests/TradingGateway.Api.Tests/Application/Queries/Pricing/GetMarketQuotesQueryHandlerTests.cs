using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingGateway.Api.Application.Queries.Pricing;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Tests.Application.Queries.Pricing
{
    public class GetMarketQuotesQueryHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ReturnsQuotesFromPricingServiceClient()
        {
            var timestamp = new DateTimeOffset(2026, 9, 17, 18, 5, 47, TimeSpan.Zero);

            var expected = new List<MarketQuoteResponse>
            {
                new()
                {
                    Symbol = "EURUSD",
                    Bid = 1.0850m,
                    Ask = 1.0852m,
                    Timestamp = timestamp
                }
            };

            var pricingServiceClient =
                new Mock<IPricingServiceClient>();

            pricingServiceClient
                .Setup(x => x.GetMarketQuotesAsync(
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var handler = new GetMarketQuotesQueryHandler(pricingServiceClient.Object);

            var result = await handler.HandleAsync(new GetMarketQuotesQuery("correlation-123"),
                CancellationToken.None);

            result.Should().BeEquivalentTo(expected);

            pricingServiceClient.Verify(
            x => x.GetMarketQuotesAsync(
                "correlation-123",
                It.IsAny<CancellationToken>()),
            Times.Once);
        }
    }
}
