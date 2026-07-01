using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using PricingService.Grpc;
using Serilog.Context;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.Prices;

namespace TradingGateway.Api.Controllers
{
    [ApiController]
    [Route("api/prices")]
    public class PricesController : ControllerBase
    {
        private readonly Pricing.PricingClient pricingClient;
        private readonly ILogger<PricesController> logger;

        public PricesController(
            Pricing.PricingClient pricingClient,
            ILogger<PricesController> logger)
        {
            this.pricingClient = pricingClient;
            this.logger = logger;
        }

        [HttpGet("{symbol}")]
        public async Task<ActionResult<PriceQuoteResponse>> GetPrice(string symbol, CancellationToken cancellationToken)
        {
            var correlationId = HttpContext.Request.Headers[CorrelationConstants.HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = HttpContext.TraceIdentifier;
            }
            var headers = new Metadata
            {
                { GrpcCorrelationConstants.MetadataKey, correlationId },
            };

            try
            {
                var response = await pricingClient.GetPriceAsync(new GetPriceRequest
                {
                    Symbol = symbol
                }, headers: headers, cancellationToken: cancellationToken);

                var result = new PriceQuoteResponse
                {
                    Symbol = symbol,
                    Bid = Convert.ToDecimal(response.Bid),
                    Ask = Convert.ToDecimal(response.Ask),
                    Mid = Convert.ToDecimal(response.Mid)
                };

                logger.LogInformation(
                "Price quote returned. Symbol={Symbol}, Bid={Bid}, Ask={Ask}, Mid={Mid}",
                result.Symbol,
                result.Bid,
                result.Ask,
                result.Mid);

                return Ok(result);
            }
            catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                logger.LogWarning(
                    exception,
                    "Invalid price quote request. Symbol={Symbol}",
                    symbol);

                return BadRequest(new
                {
                    error = exception.Status.Detail
                });
            }
            catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                logger.LogWarning(
                    exception,
                    "Price quote not found. Symbol={Symbol}",
                    symbol);

                return NotFound(new
                {
                    error = exception.Status.Detail
                });
            }
            catch (RpcException exception) when (exception.StatusCode == Grpc.Core.StatusCode.Unavailable)
            {
                logger.LogError(
                    exception,
                    "PricingService is unavailable. Symbol={Symbol}",
                    symbol);

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    error = "Pricing service is unavailable."
                });
            }
        }
    }
}
