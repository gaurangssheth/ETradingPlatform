using System;
namespace TradingApp.Shared.Validation.Rules
{
    public sealed class GreaterThanOrEqualNullableRule : IFieldValidationRule<decimal?>
    {
        private readonly decimal? threshold;

        public GreaterThanOrEqualNullableRule(decimal? threshold)
        {
            this.threshold = threshold;
        }

        public string? Validate(string fieldName, decimal? value)
        {
            if (!value.HasValue)
            {
                return $"{fieldName} is required.";
            }

            return value.Value.CompareTo(threshold) >= 0
                ? null
                : $"{fieldName} must be greater than or equal to {threshold}.";
        }
    }
}
