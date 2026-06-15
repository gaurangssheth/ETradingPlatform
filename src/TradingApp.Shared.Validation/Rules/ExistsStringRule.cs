namespace TradingApp.Shared.Validation.Rules;

public sealed class ExistsStringRule : IFieldValidationRule<string?>
{
    public string? Validate(string fieldName, string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? $"{fieldName} is required."
            : null;
    }
}