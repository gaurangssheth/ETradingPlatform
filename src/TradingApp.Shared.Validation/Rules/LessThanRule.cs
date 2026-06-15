namespace TradingApp.Shared.Validation.Rules;

public sealed class LessThanRule<T> : IFieldValidationRule<T>
    where T : IComparable<T>
{
    private readonly T threshold;

    public LessThanRule(T threshold)
    {
        this.threshold = threshold;
    }

    public string? Validate(string fieldName, T value)
    {
        return value.CompareTo(threshold) < 0
            ? null
            : $"{fieldName} must be less than {threshold}.";
    }
}