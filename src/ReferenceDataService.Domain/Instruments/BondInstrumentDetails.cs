using ReferenceDataService.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Instruments
{
    public sealed class BondInstrumentDetails : IInstrumentDetails
    {
        public BondInstrumentDetails(
            Guid instrumentId,
            string isin,
            string issuer,
            decimal couponRate,
            DateOnly maturityDate,
            decimal parValue,
            DayCountConvention dayCountConvention)
        {
            if (maturityDate <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maturityDate),
                    "Maturity date must be in the future.");
            }

            InstrumentId = Guard.ArgumentEmpty(instrumentId, nameof(instrumentId), "Instrument ID cannot be empty.");
            Isin = Guard.ArgumentNullOrWhiteSpace(isin, nameof(isin), "ISIN cannot be empty or whitespace.").ToUpperInvariant();
            Issuer = Guard.ArgumentNullOrWhiteSpace(issuer, nameof(issuer), "Issuer cannot be empty or whitespace.");
            CouponRate = Guard.ArgumentNegative(couponRate, nameof(couponRate), "Coupon rate cannot be negative.");
            MaturityDate = maturityDate;
            ParValue = Guard.ArgumentZeroOrNegative(parValue, nameof(parValue), "Par value must be greater than zero.");

            if (!Enum.IsDefined(dayCountConvention))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dayCountConvention),
                    "Day count convention is not supported.");
            }

            DayCountConvention = dayCountConvention;
        }

        public Guid InstrumentId { get; }

        public string Isin { get; }

        public string Issuer { get; }

        public decimal CouponRate { get; }

        public DateOnly MaturityDate { get; }

        public decimal ParValue { get; }

        public DayCountConvention DayCountConvention { get; }
    }
}
