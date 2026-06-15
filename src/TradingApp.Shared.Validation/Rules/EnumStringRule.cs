using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.Shared.Validation.Rules
{
    public sealed class EnumStringRule<TEnum> : IFieldValidationRule<string?>
        where TEnum : struct, Enum
    {
        public string? Validate(string fieldName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Value is required.";
            }

            if (!Enum.TryParse<TEnum>(value, true, out var parsed))
            {
                return $"Value '{value}' is not valid for {typeof(TEnum).Name}.";
            }

            if (!Enum.IsDefined(typeof(TEnum), parsed))
            {
                return $"Value '{value}' is not defined for {typeof(TEnum).Name}.";
            }

            return null;
        }
    }
}
