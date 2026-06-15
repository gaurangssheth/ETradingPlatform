using TradingApp.Shared.Validation;
using TradingApp.Shared.Validation.Rules;
using TradingApp.Contracts.Shared;
using TradingGateway.Api.Application.Commands.SubmitOrder;
using TradingGateway.Api.Models;

namespace TradingGateway.Api.Validation
{
    public sealed class SubmitOrderCommandValidator : PolymorphicValidatorBase<SubmitOrderCommand>
    {
        private readonly CompositeValidator<SubmitOrderCommand> validator;

        public SubmitOrderCommandValidator()
        {
            validator = new CompositeValidator<SubmitOrderCommand>(
                new IObjectValidationRule<SubmitOrderCommand>[]
                {
                    new FieldRule<SubmitOrderCommand, string?>("ClientId", x => x.ClientId, new ExistsStringRule()),
                    new FieldRule<SubmitOrderCommand, string?>("Symbol", x => x.Symbol, new ExistsStringRule()),
                    new FieldRule<SubmitOrderCommand, string?>("Side", x => x.Side, new ExistsStringRule()),
                    new FieldRule<SubmitOrderCommand, decimal?>("Quantity", x => x.Quantity, new GreaterThanOrEqualNullableRule(0m) ),
                    new FieldRule<SubmitOrderCommand, string?>("OrderType", x => x.OrderType, new ExistsStringRule()),
                    new FieldRule<SubmitOrderCommand, string?>("CorrelationId", x => x.CorrelationId, new ExistsStringRule()),
                    new FieldRule<SubmitOrderCommand, string?>("Side", x => x.Side, new EnumStringRule<OrderSide>()),
                    new FieldRule<SubmitOrderCommand, string?>("OrderType", x => x.OrderType, new EnumStringRule<OrderType>())

                }
            );
        }

        protected override ValidationResult Validate(SubmitOrderCommand payload)
        {
            return validator.Validate(payload);
        }
    }
}
