using Microsoft.AspNetCore.Mvc;
using TradingGateway.Api.Application.Queries;
using TradingApp.Shared.Correlation;
using TradingGateway.Api.Application.Queries.Positions;
using TradingGateway.Api.ClientModels;

namespace TradingGateway.Api.Controllers
{
    [ApiController]
    [Route("api/positions")]
    public class PositionsController : ControllerBase
    {
        private IQueryDispatcher queryDispatcher;

        public PositionsController(IQueryDispatcher queryDispatcher)
        {
            this.queryDispatcher = queryDispatcher;
        }

        [HttpGet("")]
        public async Task<ActionResult<IReadOnlyList<PositionSummaryResponse>>> GetOpenPositionsByClientId(
            [FromQuery] string clientId, CancellationToken cancellationToken)
        {
            var correlationId = HttpContext.Items[CorrelationConstants.HeaderName]?.ToString()
                ?? HttpContext.TraceIdentifier;

            var query = new GetOpenPositionsByClientIdQuery(clientId, correlationId);

            var positions = await queryDispatcher.SendAsync<
                    GetOpenPositionsByClientIdQuery,
                    IReadOnlyList< PositionSummaryResponse>>(
                        query, cancellationToken);

            return Ok(positions);
        }
    }
}
