namespace TradingApp.Shared.Validation.Rules;

public sealed class NotInRule<T> : IFieldValidationRule<T>
{
    private readonly HashSet<T> blockedValues;

    public NotInRule(IEnumerable<T> blockedValues)
    {
        this.blockedValues = new HashSet<T>(blockedValues);
    }

    public string? Validate(string fieldName, T value)
    {
        return blockedValues.Contains(value)
            ? $"{fieldName} contains an invalid value."
            : null;
    }
}