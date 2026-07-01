using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using PricingService.Grpc;
using PricingService.Grpc.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Correlation;
using TradingApp.Shared.Messaging.Correlation;

namespace PricingService.Tests
{
    public class PricingGrpcServiceTests
    {
        [Fact]
        public async Task GetPrice_Should_Return_EurUsd_Price()
        {
            var service = new PricingGrpcService(NullLogger<PricingGrpcService>.Instance);

            var response = await service.GetPrice(new Grpc.GetPriceRequest
            {
                Symbol = "EURUSD"
            }, TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().BeApproximately(1.0850, 0.0000001);
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
            response.Should().BeOfType<Grpc.GetPriceResponse>();
        }

        [Fact]
        public async Task GetPrice_Should_Normalise_Symbol()
        {
            var service = new PricingGrpcService(
                NullLogger<PricingGrpcService>.Instance);

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = " eurusd " },
                TestServerCallContext.Create());

            response.Symbol.Should().Be("EURUSD");
            response.Mid.Should().Be(1.0850);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_Symbol_Is_Unknown()
        {
            var service = new PricingGrpcService(
                NullLogger<PricingGrpcService>.Instance);

            Func<Task> action = async () => await service.GetPrice(
                new GetPriceRequest { Symbol = "ABCXYZ" },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.NotFound);
        }

        [Fact]
        public async Task GetPrice_Should_Throw_When_Symbol_Is_Empty()
        {
            var service = new PricingGrpcService(
                NullLogger<PricingGrpcService>.Instance);

            var action = async () => await service.GetPrice(
                    new GetPriceRequest { Symbol = "" },
                    TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        }

        [Fact]
        public async Task GetPrice_WhenCorrelationIdHeaderExists_ShouldStillReturnPrice()
        {
            var service = new PricingGrpcService(
                NullLogger<PricingGrpcService>.Instance);

            var headers = new Metadata
            {
                { GrpcCorrelationConstants.MetadataKey, "pricing-service-test-001" }
            };

            var response = await service.GetPrice(
                new GetPriceRequest { Symbol = "EURUSD" },
                TestServerCallContext.Create(headers));

            response.Symbol.Should().Be("EURUSD");
            response.Bid.Should().BeApproximately(1.0849, 0.0000001);
            response.Ask.Should().BeApproximately(1.0851, 0.0000001);
        }
    }
}
