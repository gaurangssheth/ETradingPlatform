namespace TradingApp.Shared.Validation;

public interface IPolymorphicValidator
{
    bool CanValidate(Type payloadType);
    ValidationResult Validate(object payload);
}