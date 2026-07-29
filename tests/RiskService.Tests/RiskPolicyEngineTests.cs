using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RiskService.Grpc.Application;
using RiskService.Grpc.Application.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Shared;

namespace RiskService.Tests
{
    public class RiskPolicyEngineTests
    {
        private readonly RiskPolicyEngine riskPolicyEngine = new(new IRiskRule[]
        {
            new ClientActiveRiskRule(),
            new ClientBlockedRiskRule(),
            new SymbolAllowedRiskRule(),
            new MaxOrderSizeRiskRule()
        }, NullLogger<RiskPolicyEngine>.Instance);

        [Fact]
        public void Check_WhenOrderPassesAllRules_ShouldApprove()
        {
            var request = CreateValidRequest();
            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeTrue();
            decision.ReasonCode.Should().Be(RiskReasonCodes.Approved);
            decision.Reason.Should().Be("Order approved by risk checks.");
            decision.RiskDecisionId.Should().NotBeEmpty();
        }

        [Fact]
        public void Check_WhenClientIsNotActive_ShouldReject()
        {
            var request = CreateValidRequest() with
            {
                ClientId = "unknown-client"
            };

            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeFalse();
            decision.ReasonCode.Should().Be(RiskReasonCodes.ClientNotActive);
            decision.Reason.Should().Be("Client unknown-client is not active.");
            decision.RiskDecisionId.Should().NotBeEmpty();
        }

        [Fact]
        public void Check_WhenSymbolIsNotAllowed_ShouldReject()
        {
            var request = CreateValidRequest() with
            {
                Symbol = "ABCXYZ"
            };

            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeFalse();
            decision.ReasonCode.Should().Be(RiskReasonCodes.SymbolNotAllowed);
            decision.Reason.Should().Be("Symbol ABCXYZ is not allowed.");
            decision.RiskDecisionId.Should().NotBeEmpty();
        }

        [Fact]
        public void Check_WhenQuantityIsZero_ShouldReject()
        {
            var request = CreateValidRequest() with
            {
                Quantity = 0m
            };

            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeFalse();
            decision.ReasonCode.Should().Be(RiskReasonCodes.InvalidQuantity);
            decision.Reason.Should().Be("Quantity must be greater than zero.");
            decision.RiskDecisionId.Should().NotBeEmpty();
        }

        [Fact]
        public void Check_WhenQuantityExceedsMaxOrderSize_ShouldReject()
        {
            var request = CreateValidRequest() with
            {
                Quantity = 1_000_001m
            };

            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeFalse();
            decision.ReasonCode.Should().Be(RiskReasonCodes.MaxOrderSizeExceeded);
            decision.Reason.Should().Be("Quantity 1000001 exceeds maximum order size 1000000.");
            decision.RiskDecisionId.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("AAPL")]
        [InlineData("GB00TEST1234")]
        public void Check_WhenMultiAssetSymbolIsAllowed_ShouldApprove(
            string symbol)
        {
            var request = CreateValidRequest() with
            {
                Symbol = symbol
            };

            var decision = riskPolicyEngine.Check(request);

            decision.Approved.Should().BeTrue();
            decision.ReasonCode.Should().Be(RiskReasonCodes.Approved);
        }

        private RiskCheckRequestModel CreateValidRequest()
        {
            return new RiskCheckRequestModel
            {
                OrderId = Guid.NewGuid(),
                ClientId = "client-001",
                Symbol = "EURUSD",
                Quantity = 100_000m,
                OrderType = OrderType.Market,
                Side = OrderSide.Buy
            };
        }
    }
}
