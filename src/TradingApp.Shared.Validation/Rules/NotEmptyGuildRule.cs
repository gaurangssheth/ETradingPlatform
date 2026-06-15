namespace TradingApp.Shared.Validation.Rules;

public sealed class NotEmptyGuidRule : IFieldValidationRule<Guid>
{
    public string? Validate(string fieldName, Guid value)
    {
        return value == Guid.Empty
            ? $"{fieldName} must not be empty."
            : null;
    }
}