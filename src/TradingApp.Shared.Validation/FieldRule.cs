namespace TradingApp.Shared.Validation;

public sealed class FieldRule<T, TValue> : IObjectValidationRule<T>
{
    private readonly string fieldName;
    private readonly Func<T, TValue> selector;
    private readonly IFieldValidationRule<TValue> rule;

    public FieldRule(string fieldName, Func<T, TValue> selector, IFieldValidationRule<TValue> rule)
    {
        this.fieldName = fieldName;
        this.selector = selector;
        this.rule = rule;
    }

    public string? Validate(T instance)
    {
        var value = selector(instance);
        return rule.Validate(fieldName, value);
    }
}