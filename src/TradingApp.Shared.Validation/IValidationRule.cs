namespace TradingApp.Shared.Validation;

public interface IValidationRule<T>
{
    string? Validate(T value);
}