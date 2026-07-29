using FluentAssertions;
using ReferenceDataService.Domain.Instruments;
using ReferenceDataService.Grpc.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DomainFxDetails =
    ReferenceDataService.Domain.Instruments.FxInstrumentDetails;

using DomainEquityDetails =
    ReferenceDataService.Domain.Instruments.EquityInstrumentDetails;

using DomainBondDetails =
    ReferenceDataService.Domain.Instruments.BondInstrumentDetails;

using PlatformAssetClass =
    TradingApp.SharedKernel.AssetClass;

using PlatformDayCountConvention =
    TradingApp.SharedKernel.DayCountConvention;

using GrpcAssetClass =
    ReferenceDataService.Grpc.AssetClass;

namespace ReferenceDataService.Grpc.Tests.Mapping
{
    public class InstrumentGrpcMapperTests
    {
        [Fact]
        public void Map_WhenInstrumentIsFx_ShouldMapCorrectly()
        {
            // Arrange

            var instrumentId = Guid.NewGuid();

            var definition = new InstrumentDefinition(
                new Instrument(
                    instrumentId,
                    "EURUSD",
                    PlatformAssetClass.Fx,
                    true),
                new DomainFxDetails(
                    instrumentId,
                    "EUR",
                    "USD",
                    0.0001m));

            var mapper = new InstrumentGrpcMapper();

            // Act

            var response = mapper.Map(definition);

            // Assert

            response.Symbol.Should().Be("EURUSD");

            response.AssetClass.Should()
                .Be(GrpcAssetClass.Fx);

            response.IsTradable.Should().BeTrue();

            response.DetailsCase.Should()
                .Be(GetInstrumentResponse.DetailsOneofCase.FxDetails);

            response.FxDetails.BaseCurrency.Should().Be("EUR");
            response.FxDetails.QuoteCurrency.Should().Be("USD");
            response.FxDetails.PipSize.Should().Be(0.0001);

            response.EquityDetails.Should().BeNull();
            response.BondDetails.Should().BeNull();
        }

        [Fact]
        public void Map_WhenInstrumentIsEquity_ShouldMapCorrectly()
        {
            var instrumentId = Guid.NewGuid();

            var definition = new InstrumentDefinition(
                new Instrument(
                    instrumentId,
                    "AAPL",
                    PlatformAssetClass.Equity,
                    true),
                new DomainEquityDetails(
                    instrumentId,
                    "NASDAQ",
                    "USD"));

            var mapper = new InstrumentGrpcMapper();

            var response = mapper.Map(definition);

            response.InstrumentId.Should().Be(instrumentId.ToString());
            response.Symbol.Should().Be("AAPL");
            response.AssetClass.Should()
                .Be(GrpcAssetClass.Equity);
            response.IsTradable.Should().BeTrue();

            response.DetailsCase.Should()
                .Be(GetInstrumentResponse.DetailsOneofCase.EquityDetails);

            response.EquityDetails.Exchange.Should().Be("NASDAQ");
            response.EquityDetails.TradingCurrency.Should().Be("USD");

            response.FxDetails.Should().BeNull();
            response.BondDetails.Should().BeNull();
        }

        [Fact]
        public void Map_WhenInstrumentIsFixedIncome_ShouldMapCorrectly()
        {
            var instrumentId = Guid.NewGuid();

            var definition = new InstrumentDefinition(
                new Instrument(
                    instrumentId,
                    "GB00TEST1234",
                    PlatformAssetClass.FixedIncome,
                    true),
                new DomainBondDetails(
                    instrumentId,
                    isin: "GB00TEST1234",
                    issuer: "UK Government",
                    denominationCurrency: "GBP",
                    couponRate: 4.25m,
                    maturityDate: new DateOnly(2035, 6, 30),
                    parValue: 100m,
                    dayCountConvention: PlatformDayCountConvention.ActualActual));

            var mapper = new InstrumentGrpcMapper();

            var response = mapper.Map(definition);

            response.InstrumentId.Should().Be(instrumentId.ToString());
            response.Symbol.Should().Be("GB00TEST1234");
            response.AssetClass.Should()
                .Be(GrpcAssetClass.FixedIncome);
            response.IsTradable.Should().BeTrue();

            response.DetailsCase.Should()
                .Be(GetInstrumentResponse.DetailsOneofCase.BondDetails);

            response.BondDetails.Isin.Should().Be("GB00TEST1234");
            response.BondDetails.Issuer.Should().Be("UK Government");
            response.BondDetails.CouponRate.Should().Be(4.25);
            response.BondDetails.MaturityDate.Should().Be("2035-06-30");
            response.BondDetails.ParValue.Should().Be(100);

            response.BondDetails.DayCountConvention.Should()
                .Be(Grpc.DayCountConvention
                    .ActualActual);

            response.BondDetails.DenominationCurrency
                .Should()
                .Be("GBP");

            response.FxDetails.Should().BeNull();
            response.EquityDetails.Should().BeNull();
        }

        [Fact]
        public void Map_WhenDefinitionIsNull_ShouldThrowArgumentNullException()
        {
            var mapper = new InstrumentGrpcMapper();

            Action action = () => mapper.Map(null!);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("definition");
        }

        [Fact]
        public void Map_WhenDetailsTypeIsUnsupported_ShouldThrowNotSupportedException()
        {
            var instrumentId = Guid.NewGuid();

            var definition = new InstrumentDefinition(
                new Instrument(
                    instrumentId,
                    "TEST",
                    PlatformAssetClass.Equity,
                    true),
                new UnsupportedInstrumentDetails(instrumentId));

            var mapper = new InstrumentGrpcMapper();

            Action action = () => mapper.Map(definition);

            action.Should()
                .Throw<NotSupportedException>()
                .WithMessage(
                    "Instrument details type 'UnsupportedInstrumentDetails' is not supported.");
        }

        private sealed class UnsupportedInstrumentDetails : IInstrumentDetails
        {
            public UnsupportedInstrumentDetails(Guid instrumentId)
            {
                InstrumentId = instrumentId;
            }

            public Guid InstrumentId { get; }
        }
    }
}
