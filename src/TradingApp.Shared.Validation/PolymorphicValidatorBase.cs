namespace TradingApp.Shared.Validation;

public abstract class PolymorphicValidatorBase<T> : IPolymorphicValidator
{
    public bool CanValidate(Type payloadType)
    {
        return typeof(T).IsAssignableFrom(payloadType);
    }

    public ValidationResult Validate(object payload)
    {
        if (payload is not T typedPayload)
        {
            throw new InvalidOperationException(
                $"Invalid payload type. Expected {typeof(T).Name}, got {payload.GetType().Name}.");
        }

        return Validate(typedPayload);
    }

    protected abstract ValidationResult Validate(T payload);
}