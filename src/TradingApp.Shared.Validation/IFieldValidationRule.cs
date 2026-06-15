namespace TradingApp.Shared.Validation;

public interface IFieldValidationRule<in TValue>
{
    string? Validate(string fieldName, TValue value);
}