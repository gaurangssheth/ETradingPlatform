using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingGateway.Api.Application.Queries.Positions;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Tests.Application.Queries.Positions
{
    public class GetOpenPositionsByClientIdQueryHandlerTests
    {
        [Fact]
        public async Task HandleAsync_ShouldCallPositionServiceClientAndReturnPositions()
        {
            var expectedPositions = new List<PositionSummaryResponse>
            {
                new(InstrumentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Symbol: "EURUSD",
                    AssetClass: "Fx",
                    NetQuantity: 100000m,
                    AveragePrice: 1.0850m,
                    PnlCurrency: "USD",
                    RealisedPnl: 25m,
                    UnrealisedPnl: 75m,
                    TotalPnl: 100m
                )
            };

            var positionServiceClient =
                new Mock<IPositionServiceClient>();

            positionServiceClient
                .Setup(client =>
                    client.GetOpenPositionsByClientIdAsync(
                        "client-001",
                        "positions-query-test-001",
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPositions);

            var handler = new GetOpenPositionsByClientIdQueryHandler(
                    positionServiceClient.Object);

            var query = new GetOpenPositionsByClientIdQuery(
                    "client-001",
                    "positions-query-test-001");

            var result = await handler.HandleAsync(
                    query,
                    CancellationToken.None);

            result.Should().BeEquivalentTo(expectedPositions);

            positionServiceClient.Verify(
                client =>
                    client.GetOpenPositionsByClientIdAsync(
                        "client-001",
                        "positions-query-test-001",
                        It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
