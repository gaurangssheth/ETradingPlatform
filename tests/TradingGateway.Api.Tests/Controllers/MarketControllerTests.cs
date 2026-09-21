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
using TradingGateway.Api.Application.Queries.Pricing;

namespace TradingGateway.Api.Tests.Controllers
{
    public class MarketControllerTests
    {
        [Fact]
        public async Task GetMarketQuotes_ShouldDispatchQueryAndReturnOk()
        {
            var quotes = new List<MarketQuoteResponse>
            {
                new MarketQuoteResponse
                {
                    Symbol = "EURUSD",
                    Bid = 1.0850m,
                    Ask = 1.0852m,
                    Timestamp = DateTimeOffset.UtcNow
                }
            };

            var queryDispatcher = new Mock<IQueryDispatcher>();

            queryDispatcher
                .Setup(q => q.SendAsync<GetMarketQuotesQuery, IReadOnlyList<MarketQuoteResponse>>(
                    It.Is<GetMarketQuotesQuery>(query =>
                        query.CorrelationId == "market-controller-test-001"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(quotes);

            var controller = new MarketController(queryDispatcher.Object);

            var httpContext = new DefaultHttpContext();

            httpContext.Items[CorrelationConstants.HeaderName] =
                "market-controller-test-001";

            controller.ControllerContext =
                new ControllerContext
                {
                    HttpContext = httpContext
                };

            var result = await controller.GetQuotes(CancellationToken.None);

            var okResult = result.Result.Should()
                .BeOfType<OkObjectResult>().Subject;

            var response = okResult.Value.Should()
                .BeAssignableTo<IReadOnlyList<MarketQuoteResponse>>().Subject;

            response.Should().ContainSingle();

            response.Single().Symbol.Should().Be("EURUSD");
            response.Single().Bid.Should().Be(1.0850m);
            response.Single().Ask.Should().Be(1.0852m);

            queryDispatcher.Verify(
                dispatcher =>
                    dispatcher.SendAsync<
                        GetMarketQuotesQuery,
                        IReadOnlyList<MarketQuoteResponse>>(
                        It.Is<GetMarketQuotesQuery>(
                            query =>
                                query.CorrelationId == "market-controller-test-001"),
                        It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
