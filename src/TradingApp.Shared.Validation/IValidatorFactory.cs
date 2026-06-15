namespace TradingApp.Shared.Validation;

public interface IValidatorFactory
{
    IPolymorphicValidator GetValidator(Type payloadType);
    ValidationResult Validate(object payload);
}