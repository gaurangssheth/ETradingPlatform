namespace TradingApp.Shared.Validation;

public sealed class CompositeValidator<T> : IValidator<T>
{
    private readonly IEnumerable<IObjectValidationRule<T>> rules;

    public CompositeValidator(IEnumerable<IObjectValidationRule<T>> rules)
    {
        this.rules = rules;
    }

    public ValidationResult Validate(T instance)
    {
        var result = new ValidationResult();

        foreach (var rule in rules)
        {
            var error = rule.Validate(instance);
            if (!string.IsNullOrWhiteSpace(error))
            {
                result.AddError(error!);
            }
        }

        return result;
    }
}