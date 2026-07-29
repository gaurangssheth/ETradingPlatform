using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReferenceDataService.Grpc.Mapping;
using ReferenceDataService.Grpc.Services;
using ReferenceDataService.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;

namespace ReferenceDataService.Grpc.Tests.Services
{
    public class ReferenceDataGrpcServiceTests
    {
        [Fact]
        public async Task GetInstrument_WhenSymbolIsFx_ShouldReturnFxInstrument()
        {
            var service = CreateService();
            var response = await service.GetInstrument(
                new GetInstrumentRequest
                {
                    Symbol = "EURUSD"
                },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.AssetClass.Should().Be(AssetClass.Fx);
            response.IsTradable.Should().BeTrue();

            response.DetailsCase
            .Should()
            .Be(GetInstrumentResponse.DetailsOneofCase.FxDetails);

            response.FxDetails.BaseCurrency.Should().Be("EUR");
            response.FxDetails.QuoteCurrency.Should().Be("USD");
            response.FxDetails.PipSize.Should().Be(0.0001);
        }

        [Fact]
        public async Task GetInstrument_WhenSymbolIsEquity_ShouldReturnEquityInstrument()
        {
            var service = CreateService();

            var response = await service.GetInstrument(
                new GetInstrumentRequest
                {
                    Symbol = "AAPL"
                },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("AAPL");
            response.AssetClass.Should().Be(AssetClass.Equity);
            response.IsTradable.Should().BeTrue();

            response.DetailsCase.Should().Be(GetInstrumentResponse.DetailsOneofCase.EquityDetails);
            response.EquityDetails.Exchange.Should().Be("NASDAQ");
            response.EquityDetails.TradingCurrency.Should().Be("USD");

            response.FxDetails.Should().BeNull();
            response.BondDetails.Should().BeNull();
        }

        [Fact]
        public async Task GetInstrument_WhenSymbolIsFixedIncome_ShouldReturnBondInstrument()
        {
            var service = CreateService();

            var response = await service.GetInstrument(
                new GetInstrumentRequest
                {
                    Symbol = "GB00TEST1234"
                },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("GB00TEST1234");
            response.AssetClass.Should().Be(AssetClass.FixedIncome);
            response.IsTradable.Should().BeTrue();

            response.DetailsCase.Should().Be(GetInstrumentResponse.DetailsOneofCase.BondDetails);
            response.BondDetails.Isin.Should().Be("GB00TEST1234");
            response.BondDetails.Issuer.Should().Be("UK Government");
            response.BondDetails.CouponRate.Should().Be(4.25);
            response.BondDetails.MaturityDate.Should().Be("2035-06-30");
            response.BondDetails.ParValue.Should().Be(100);
            response.BondDetails.DenominationCurrency.Should().Be("GBP");

            response.BondDetails.DayCountConvention
            .Should()
            .Be(DayCountConvention.ActualActual);

            response.FxDetails.Should().BeNull();
            response.EquityDetails.Should().BeNull();
        }

        [Fact]
        public async Task GetInstrument_WhenSymbolDoesNotExist_ShouldThrowNotFoundRpcException()
        {
            var service = CreateService();

            Func<Task> action = async () => await service.GetInstrument(
                new GetInstrumentRequest
                {
                    Symbol = "UNKNOWN"
                },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode
                .Should()
                .Be(StatusCode.NotFound);

            exception.Which.Status.Detail
                .Should()
                .Be("Instrument 'UNKNOWN' was not found.");
        }

        private static ReferenceDataGrpcService CreateService(
            ILogger<ReferenceDataGrpcService>? logger = null)
        {
            var repository = new InMemoryInstrumentRepository();

            IInstrumentGrpcMapper instrumentMapper = new InstrumentGrpcMapper();

            var service = new ReferenceDataGrpcService(repository,
                instrumentMapper,
                logger ?? NullLogger<ReferenceDataGrpcService>.Instance);

            return service;
        }
    }
}
