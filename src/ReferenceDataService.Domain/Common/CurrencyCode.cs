using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace ReferenceDataService.Domain.Common
{
    public readonly record struct CurrencyCode
    {
        public CurrencyCode(string value)
        {
            Value = Guard.ArgumentNullOrWhiteSpace(
                value,
                nameof(value),
                "Currency code cannot be null or whitespace.")
                .ToUpperInvariant();
        }

        public string Value { get; }
    }
}
