using NServiceBus.TransactionalSession;
using OrderService.Application.Commands;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Validation;

namespace TradingGateway.Api.Application.Commands.SubmitOrder
{
    public sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand, SubmitOrderResult>
    {
        private readonly IValidatorFactory validatorFactory;
        private readonly ITransactionalSession transactionalSession;
        private readonly ILogger<SubmitOrderCommandHandler> logger;

        public SubmitOrderCommandHandler(IValidatorFactory validatorFactory, 
            ITransactionalSession transactionalSession,
            ILogger<SubmitOrderCommandHandler> logger)
        {
            this.validatorFactory = validatorFactory;
            this.transactionalSession = transactionalSession;
            this.logger = logger;
        }

        public async Task<SubmitOrderResult> HandleAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
        {
            var validationResult = this.validatorFactory.Validate(command);
            if (!validationResult.IsValid)
            {
                return new SubmitOrderResult
                {
                    Accepted = false,
                    Status = "ValidationFailed",
                    Error = string.Join("; ", validationResult.Errors),
                    CorrelationId = command.CorrelationId!
                };
            }

            var side = Enum.Parse<OrderSide>(command.Side!, true);
            var orderType = Enum.Parse<OrderType>(command.OrderType!, true);
            var orderId = Guid.NewGuid();

            logger.LogInformation(
                "Submitting order. ClientId={ClientId}, Symbol={Symbol}, Side={Side}, Quantity={Quantity}, OrderType={OrderType}, CorrelationId={CorrelationId}",
                command.ClientId,
                command.Symbol,
                command.Side,
                command.Quantity,
                command.OrderType,
                command.CorrelationId);

            await transactionalSession
            .Open(new SqlPersistenceOpenSessionOptions(), cancellationToken);

            await transactionalSession.Send(new TradingApp.Contracts.Commands.SubmitOrder
            {
                OrderId = orderId,
                ClientId = command.ClientId!,
                Symbol = command.Symbol!,
                Side = side,
                Quantity = command.Quantity!.Value,
                OrderType = orderType,
                CorrelationId = command.CorrelationId!

            }, cancellationToken);

            await transactionalSession.Commit(cancellationToken);

            logger.LogInformation(
            "SubmitOrder message sent. OrderId={OrderId}, CorrelationId={CorrelationId}",
            orderId,
            command.CorrelationId);

            return new SubmitOrderResult
            {
                OrderId = orderId,
                Accepted = true,
                Status = "Submitted",
                CorrelationId = command.CorrelationId
            };

        }
    }
}
