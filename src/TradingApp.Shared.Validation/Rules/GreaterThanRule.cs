namespace TradingApp.Shared.Validation.Rules;

public sealed class GreaterThanRule<T> : IFieldValidationRule<T>
    where T : IComparable<T>
{
    private readonly T threshold;

    public GreaterThanRule(T threshold)
    {
        this.threshold = threshold;
    }

    public string? Validate(string fieldName, T value)
    {
        return value.CompareTo(threshold) > 0
            ? null
            : $"{fieldName} must be greater than {threshold}.";
    }
}