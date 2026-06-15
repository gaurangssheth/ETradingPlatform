namespace TradingApp.Shared.Validation.Rules;

public sealed class LessThanOrEqualRule<T> : IFieldValidationRule<T>
    where T : IComparable<T>
{
    private readonly T threshold;

    public LessThanOrEqualRule(T threshold)
    {
        this.threshold = threshold;
    }

    public string? Validate(string fieldName, T value)
    {
        return value.CompareTo(threshold) <= 0
            ? null
            : $"{fieldName} must be less than or equal to {threshold}.";
    }
}