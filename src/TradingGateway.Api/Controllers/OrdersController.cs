using Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OrderService.Application.Commands;
using OrderService.Application.Queries;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Validation;
using TradingGateway.Api.Application.Commands.SubmitOrder;
using TradingGateway.Api.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ICommandDispatcher commandDispatcher;
    private readonly IQueryDispatcher queryDispatcher;
    private readonly ILogger<OrdersController> logger;

    public OrdersController(
        IValidatorFactory validatorFactory,
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        ILogger<OrdersController> logger)
    {
        this.commandDispatcher = commandDispatcher;
        this.queryDispatcher = queryDispatcher;
        this.logger = logger;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitOrder([FromBody] SubmitOrderRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HeaderName]?.ToString()
            ?? HttpContext.TraceIdentifier;

        var command = new SubmitOrderCommand
        (
            request.ClientId,
            request.Symbol,
            request.Side,
            request.Quantity,
            request.OrderType,
            correlationId
        );

        var result = await commandDispatcher.SendAsync<
            SubmitOrderCommand,
            SubmitOrderResult>(
                command,
                cancellationToken);

        if (!result.Accepted)
        {
            logger.LogWarning("SubmitOrder failed: {Error}", result.Error);
            return BadRequest(result);
        }

        return Accepted(result);
    }
}

