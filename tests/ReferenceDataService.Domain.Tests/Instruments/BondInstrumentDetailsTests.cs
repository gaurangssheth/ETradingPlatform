using FluentAssertions;
using ReferenceDataService.Domain.Instruments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReferenceDataService.Domain.Tests.Instruments
{
    public class BondInstrumentDetailsTests
    {
        [Fact]
        public void Create_WithValidValues_ShouldCreateBondInstrumentDetails()
        {
            var instrumentId = Guid.NewGuid();
            var maturityDate = new DateOnly(2035, 6, 30);

            var details = new BondInstrumentDetails(
            instrumentId,
            isin: "GB00TEST1234",
            issuer: "UK Government",
            couponRate: 4.25m,
            maturityDate: maturityDate,
            parValue: 100m,
            dayCountConvention: DayCountConvention.ActualActual);

            details.InstrumentId.Should().Be(instrumentId);
            details.Isin.Should().Be("GB00TEST1234");
            details.Issuer.Should().Be("UK Government");
            details.CouponRate.Should().Be(4.25m);
            details.MaturityDate.Should().Be(maturityDate);
            details.ParValue.Should().Be(100m);
            details.DayCountConvention.Should().Be(DayCountConvention.ActualActual);
        }
    }
}
