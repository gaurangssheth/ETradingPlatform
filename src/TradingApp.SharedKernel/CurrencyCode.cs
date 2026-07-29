using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.SharedKernel
{
    public readonly record struct CurrencyCode
    {
        public CurrencyCode(string value)
        {
            var normalizedValue = Guard.ArgumentNullOrWhiteSpace(
                value,
                nameof(value),
                "Currency code cannot be null or whitespace.")
            .ToUpperInvariant();

            if (normalizedValue.Length != 3 ||
                !normalizedValue.All(char.IsAsciiLetterUpper))
            {
                throw new ArgumentException(
                    "Currency code must contain exactly three letters.",
                    nameof(value));
            }

            Value = normalizedValue;
        }

        public string Value { get; }

        //public override string ToString() => Value;
    }
}
