using Microsoft.AspNetCore.Mvc;
using TradingApp.Shared.Correlation;
using TradingGateway.Api.Application.Queries;
using TradingGateway.Api.Application.Queries.Pricing;
using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Controllers
{
    [ApiController]
    [Route("api/market")]
    public class MarketController : ControllerBase
    {
        private readonly IQueryDispatcher queryDispatcher;

        public MarketController(IQueryDispatcher queryDispatcher)
        {
            this.queryDispatcher = queryDispatcher;
        }

        [HttpGet("quotes")]
        public async Task<ActionResult<IReadOnlyList<MarketQuoteResponse>>> GetQuotes(
            CancellationToken cancellationToken)
        {
            var correlationId = HttpContext.Items[CorrelationConstants.HeaderName]?.ToString()
                ?? HttpContext.TraceIdentifier;

            var result = await queryDispatcher.SendAsync<GetMarketQuotesQuery, 
                IReadOnlyList<MarketQuoteResponse>>(new GetMarketQuotesQuery(correlationId),
                cancellationToken);

            return Ok(result);
        }
    }
}
