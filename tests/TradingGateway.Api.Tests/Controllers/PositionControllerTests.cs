using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TradingGateway.Api.Application.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Correlation;
using TradingGateway.Api.Application.Queries.Positions;
using TradingGateway.Api.ClientModels;
using TradingGateway.Api.Controllers;

namespace TradingGateway.Api.Tests.Controllers
{
    public class PositionControllerTests
    {
        [Fact]
        public async Task GetOpenPositionsByClientId_ShouldDispatchQueryAndReturnOk()
        {
            var positions = new List<PositionSummaryResponse>
            {
                new PositionSummaryResponse(
                    InstrumentId: Guid.NewGuid(),
                    Symbol: "EURUSD",
                    AssetClass: "Fx",
                    NetQuantity: 100000,
                    AveragePrice: 1.0850m,
                    PnlCurrency: "USD",
                    RealisedPnl: 25.00m,
                    UnrealisedPnl: 75.00m,
                    TotalPnl: 100.00m
                )
            };

            var queryDispatcher = new Mock<IQueryDispatcher>();

            queryDispatcher
                .Setup(q => q.SendAsync<GetOpenPositionsByClientIdQuery, IReadOnlyList<PositionSummaryResponse>>(
                    It.Is<GetOpenPositionsByClientIdQuery>(query =>
                        query.ClientId == "client-001" &&
                        query.CorrelationId == "positions-controller-test-001"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(positions);

            var controller = new PositionsController(queryDispatcher.Object);

            var httpContext = new DefaultHttpContext();

            httpContext.Items[CorrelationConstants.HeaderName] =
                "positions-controller-test-001";

            controller.ControllerContext =
                new ControllerContext
                {
                    HttpContext = httpContext
                };

            var result = await controller.GetOpenPositionsByClientId("client-001", CancellationToken.None);

            var okResult = result.Result.Should()
                .BeOfType<OkObjectResult>().Subject;

            var response = okResult.Value.Should()
                .BeAssignableTo<IReadOnlyList<PositionSummaryResponse>>().Subject;

            response.Should().ContainSingle();

            response.Single().Symbol.Should().Be("EURUSD");
            response.Single().TotalPnl.Should().Be(100m);

            queryDispatcher.Verify(
                dispatcher =>
                    dispatcher.SendAsync<
                        GetOpenPositionsByClientIdQuery,
                        IReadOnlyList<PositionSummaryResponse>>(
                        It.Is<GetOpenPositionsByClientIdQuery>(
                            query =>
                                query.ClientId == "client-001" &&
                                query.CorrelationId == "positions-controller-test-001"),
                        It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
