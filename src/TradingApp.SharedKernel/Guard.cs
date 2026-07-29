using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingApp.SharedKernel
{
    public static class Guard
    {
        public static string ArgumentNullOrWhiteSpace(
            string value,
            string parameterName,
            string message)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    message,
                    parameterName);
            }

            return value.Trim();
        }

        public static Guid ArgumentEmpty(
            Guid value,
            string parameterName,
            string message)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);

            if (value == Guid.Empty)
            {
                throw new ArgumentException(
                    message,
                    parameterName);
            }

            return value;
        }

        public static decimal ArgumentZeroOrNegative(
            decimal value,
            string parameterName,
            string message)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    message);
            }

            return value;
        }

        public static decimal ArgumentNegative(
            decimal value,
            string parameterName,
            string message)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    message);
            }

            return value;
        }

        public static DateOnly ArgumentNotInFuture(
            DateOnly value,
            string parameterName,
            string message)
        {
            if (value <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    message);
            }
            return value;
        }
    }
}
