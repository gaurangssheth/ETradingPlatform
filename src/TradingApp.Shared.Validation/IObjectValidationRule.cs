namespace TradingApp.Shared.Validation;

public interface IObjectValidationRule<in T>
{
    string? Validate(T instance);
}