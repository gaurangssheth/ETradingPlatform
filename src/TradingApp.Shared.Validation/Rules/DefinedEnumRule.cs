namespace TradingApp.Shared.Validation.Rules;

public sealed class DefinedEnumRule<TEnum> : IFieldValidationRule<TEnum>
    where TEnum : struct, Enum
{
    public string? Validate(string fieldName, TEnum value)
    {
        return Enum.IsDefined(typeof(TEnum), value)
            ? null
            : $"{fieldName} is not a valid {typeof(TEnum).Name}.";
    }
}