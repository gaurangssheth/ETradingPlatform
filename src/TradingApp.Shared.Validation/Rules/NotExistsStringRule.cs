namespace TradingApp.Shared.Validation.Rules;

public sealed class NotExistsStringRule : IFieldValidationRule<string?>
{
    public string? Validate(string fieldName, string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? $"{fieldName} must not be provided."
            : null;
    }
}