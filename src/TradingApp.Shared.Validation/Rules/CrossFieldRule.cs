using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.Shared.Validation.Rules
{
    public sealed class CrossFieldRule<T, TValue> : IValidationRule<T>
    {
        private readonly Func<T, TValue> selector;
        private readonly IValidationRule<TValue> rule;

        public CrossFieldRule(
            Func<T, TValue> selector,
            IValidationRule<TValue> rule)
        {
            this.selector = selector;
            this.rule = rule;
        }

        public string? Validate(T value)
        {
            var selectedValue = this.selector(value);
            return rule.Validate(selectedValue);
        }
    }
}
