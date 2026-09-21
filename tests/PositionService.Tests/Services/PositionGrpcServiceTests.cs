using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PositionService.Application.Positions;
using PositionService.Application.Queries;
using PositionService.Grpc;
using PositionService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;
using TradingApp.SharedKernel;

namespace PositionService.Tests.Services
{
    public class PositionGrpcServiceTests
    {
        [Fact]
        public async Task GetOpenPositionsByClientId_ShouldReturnMappedPositions()
        {
            var queryDispatcher = new Mock<IQueryDispatcher>();

            var positions = new List<PositionSummaryReadModel>
            {
                new(
                    InstrumentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Symbol: "EURUSD",
                    AssetClass: AssetClass.Fx,
                    NetQuantity: 100000m,
                    AveragePrice: 1.0850m,
                    PnlCurrency: new CurrencyCode("USD"),
                    RealisedPnl: 25m,
                    UnrealisedPnl: 75m
                )
            };

            queryDispatcher.Setup(dispatcher => dispatcher.SendAsync<GetOpenPositionsByClientIdQuery, IReadOnlyList<PositionSummaryReadModel>>(
                It.Is<GetOpenPositionsByClientIdQuery>(query => query.ClientId == "client-001"),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(positions);

            var service = new PositionGrpcService(queryDispatcher.Object,
                NullLogger<PositionGrpcService>.Instance);

            var headers = new Metadata
            {
                {
                    GrpcCorrelationConstants.MetadataKey,
                    "position-test-001"
                }
            };

            var context = TestServerCallContext.Create(headers);

            var response = await service.GetOpenPositionsByClientId(new Grpc.GetOpenPositionsByClientIdRequest
            {
                ClientId = "client-001"
            }, context);

            response.Positions.Should().ContainSingle();
            var position = response.Positions.Single();

            position.InstrumentId.Should()
                .Be("11111111-1111-1111-1111-111111111111");

            position.Symbol.Should().Be("EURUSD");
            position.AssetClass.Should().Be("Fx");
            position.NetQuantity.Should().Be("100000");
            position.AveragePrice.Should().Be("1.0850");
            position.PnlCurrency.Should().Be("USD");
            position.RealisedPnl.Should().Be("25");
            position.UnrealisedPnl.Should().Be("75");
            position.TotalPnl.Should().Be("100");
        }

        [Fact]
        public async Task GetOpenPositionsByClientId_WhenCorrelationIdIsMissing_ReturnsPositions()
        {
            var queryDispatcher = new Mock<IQueryDispatcher>();

            queryDispatcher
                .Setup(x => x.SendAsync<
                    GetOpenPositionsByClientIdQuery,
                    IReadOnlyList<PositionSummaryReadModel>>(
                        It.IsAny<GetOpenPositionsByClientIdQuery>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<PositionSummaryReadModel>());

            var service = new PositionGrpcService(
                queryDispatcher.Object,
                NullLogger<PositionGrpcService>.Instance);

            var context =
                TestServerCallContext.Create();

            var response =
                await service.GetOpenPositionsByClientId(
                    new GetOpenPositionsByClientIdRequest
                    {
                        ClientId = "client-001",
                    },
                    context);

            response.Positions.Should().BeEmpty();
        }
    }
}
