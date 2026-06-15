namespace TradingApp.Shared.Validation;

public interface IValidator<in T>
{
    ValidationResult Validate(T instance);
}