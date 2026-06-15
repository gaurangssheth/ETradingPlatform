namespace TradingApp.Shared.Validation.Rules;

public sealed class InRule<T> : IFieldValidationRule<T>
{
    private readonly HashSet<T> allowedValues;

    public InRule(IEnumerable<T> allowedValues)
    {
        this.allowedValues = new HashSet<T>(allowedValues);
    }

    public string? Validate(string fieldName, T value)
    {
        return allowedValues.Contains(value)
            ? null
            : $"{fieldName} must be one of: {string.Join(", ", allowedValues)}.";
    }
}