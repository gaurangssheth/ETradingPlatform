using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using RiskService.Grpc;
using RiskService.Grpc.Application;
using RiskService.Grpc.Application.Rules;
using RiskService.Grpc.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;

namespace RiskService.Tests
{
    public class RiskGrpcServiceTests
    {
        [Fact]
        public async Task CheckOrderRisk_Should_Approve_Valid_Order()
        {
            var service = CreateService();

            var response = await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = Guid.NewGuid().ToString(),
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = "Buy",
                    Quantity = 100_000,
                    OrderType = "Market"
                }, 
                TestServerCallContext.Create());

            response.Approved.Should().BeTrue();
            response.ReasonCode.Should().Be(RiskReasonCodes.Approved);
            response.Reason.Should().Be("Order approved by risk checks.");
            response.RiskDecisionId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CheckOrderRisk_Should_Normalise_Symbol()
        {
            var service = CreateService();

            var response = await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = Guid.NewGuid().ToString(),
                    ClientId = "client-001",
                    Symbol = " eurusd ",
                    Side = "Buy",
                    Quantity = 100_000,
                    OrderType = "Market"
                },
                TestServerCallContext.Create());

            response.Approved.Should().BeTrue();
            response.ReasonCode.Should().Be(RiskReasonCodes.Approved);
        }

        [Fact]
        public async Task CheckOrderRisk_Should_Reject_Inactive_Client()
        {
            var service = CreateService();

            var response = await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = Guid.NewGuid().ToString(),
                    ClientId = "unknown-client",
                    Symbol = "EURUSD",
                    Side = "Buy",
                    Quantity = 100_000,
                    OrderType = "Market"
                },
                TestServerCallContext.Create());

            response.Approved.Should().BeFalse();
            response.ReasonCode.Should().Be(RiskReasonCodes.ClientNotActive);
            response.Reason.Should().Be("Client unknown-client is not active.");
            response.RiskDecisionId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task CheckOrderRisk_Should_Throw_When_OrderId_Is_Invalid()
        {
            var service = CreateService();

            var action = async () => await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = "not-a-guid",
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = "Buy",
                    Quantity = 100_000,
                    OrderType = "Market"
                },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
            exception.Which.Status.Detail.Should().Be("OrderId must be a valid GUID.");
        }

        [Fact]
        public async Task CheckOrderRisk_Should_Throw_When_Side_Is_Invalid()
        {
            var service = CreateService();

            var action = async () => await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = Guid.NewGuid().ToString(),
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = "WrongSide",
                    Quantity = 100_000,
                    OrderType = "Market"
                },
                TestServerCallContext.Create());

            var exception = await action.Should().ThrowAsync<RpcException>();

            exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
            exception.Which.Status.Detail.Should().Contain("Side must be a valid value");
        }

        [Fact]
        public async Task CheckOrderRisk_WhenCorrelationIdHeaderExists_ShouldStillReturnDecision()
        {
            var service = CreateService();

            var headers = new Metadata
            {
                { GrpcCorrelationConstants.MetadataKey, "risk-service-test-001" }
            };

            var response = await service.CheckOrderRisk(
                new CheckOrderRiskRequest
                {
                    OrderId = Guid.NewGuid().ToString(),
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = "Buy",
                    Quantity = 100_000,
                    OrderType = "Market"
                },
                TestServerCallContext.Create(headers));

            response.Approved.Should().BeTrue();
            response.ReasonCode.Should().Be(RiskReasonCodes.Approved);
        }

        private static RiskGrpcService CreateService()
        {
            var riskPolicyEngine = new RiskPolicyEngine(
            new IRiskRule[]
            {
                new ClientActiveRiskRule(),
                new ClientBlockedRiskRule(),
                new SymbolAllowedRiskRule(),
                new MaxOrderSizeRiskRule()
            },
            NullLogger<RiskPolicyEngine>.Instance);

            return new RiskGrpcService(
                riskPolicyEngine,
                NullLogger<RiskGrpcService>.Instance);
        }
    }
}
