namespace TradingApp.Shared.Validation;

public sealed class ValidatorFactory : IValidatorFactory
{
    private readonly IReadOnlyCollection<IPolymorphicValidator> validators;

    public ValidatorFactory(IEnumerable<IPolymorphicValidator> validators)
    {
        this.validators = validators.ToList().AsReadOnly();
    }

    public IPolymorphicValidator GetValidator(Type payloadType)
    {
        var validator = validators.FirstOrDefault(v => v.CanValidate(payloadType));

        if (validator is null)
        {
            throw new InvalidOperationException(
                $"No validator registered for payload type {payloadType.Name}.");
        }

        return validator;
    }

    public ValidationResult Validate(object payload)
    {
        if (payload is null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        var validator = GetValidator(payload.GetType());
        return validator.Validate(payload);
    }
}