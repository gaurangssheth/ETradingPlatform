using System.Text.RegularExpressions;

namespace TradingApp.Shared.Validation.Rules;

public sealed class RegexMatchRule : IFieldValidationRule<string?>
{
    private readonly Regex regex;
    private readonly string errorMessage;

    public RegexMatchRule(string pattern, string errorMessage)
    {
        regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        this.errorMessage = errorMessage;
    }

    public string? Validate(string fieldName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return regex.IsMatch(value)
            ? null
            : $"{fieldName} {errorMessage}";
    }
}