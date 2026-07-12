using FluentAssertions;
using Grpc.Core;
using Moq;
using OrderService.Risk;
using RiskService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Shared;
using TradingApp.Shared.Messaging.Correlation;

namespace OrderService.Tests.Risk
{
    public class GrpcRiskClientTests
    {
        [Fact]
        public async Task CheckOrderRiskAsync_Should_Call_Grpc_Client_And_Return_RiskCheckResult()
        {
            var orderId = Guid.NewGuid();
            var riskDecisionId = Guid.NewGuid();

            var grpcResponse = new RiskService.Grpc.CheckOrderRiskResponse
            {
                Approved = true,
                ReasonCode = "APPROVED",
                Reason = "Order approved by risk checks.",
                RiskDecisionId = riskDecisionId.ToString()
            };

            var grpcClient = new Mock<RiskService.Grpc.Risk.RiskClient>();

            grpcClient.Setup(x => x.CheckOrderRiskAsync(
                It.Is<CheckOrderRiskRequest>(
                    r => r.OrderId == orderId.ToString()
                    && r.ClientId == "client-001"
                    && r.Symbol == "EURUSD"
                    && r.Side == "Buy"
                    && r.Quantity == 100000
                    && r.OrderType == "Market"
                    ), 
                It.IsAny<Metadata>(), 
                It.IsAny<DateTime?>(), 
                It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcRiskClient(grpcClient.Object);

            var result = await client.CheckOrderRiskAsync(
                new SubmitOrder
                {
                    OrderId = orderId,
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = OrderSide.Buy,
                    Quantity = 100000m,
                    OrderType = OrderType.Market,
                    CorrelationId = "risk-client-test-001"
                }, CancellationToken.None);

            result.Approved.Should().BeTrue();
            result.ReasonCode.Should().Be("APPROVED");
            result.Reason.Should().Be("Order approved by risk checks.");
            result.RiskDecisionId.Should().Be(riskDecisionId);

            grpcClient.Verify(x => x.CheckOrderRiskAsync(
                    It.IsAny<RiskService.Grpc.CheckOrderRiskRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CheckOrderRiskAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            Metadata? capturedHeaders = null;

            var grpcResponse = new RiskService.Grpc.CheckOrderRiskResponse
            {
                Approved = true,
                ReasonCode = "APPROVED",
                Reason = "Order approved by risk checks.",
                RiskDecisionId = Guid.NewGuid().ToString()
            };

            var grpcClient = new Mock<RiskService.Grpc.Risk.RiskClient>();

            grpcClient
            .Setup(x => x.CheckOrderRiskAsync(
                It.IsAny<RiskService.Grpc.CheckOrderRiskRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<RiskService.Grpc.CheckOrderRiskRequest, Metadata, DateTime?, CancellationToken>(
                (_, headers, _, _) =>
                {
                    capturedHeaders = headers;
                })
            .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcRiskClient(grpcClient.Object);

            await client.CheckOrderRiskAsync(
            new SubmitOrder
            {
                OrderId = Guid.NewGuid(),
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                Quantity = 100000m,
                OrderType = OrderType.Market,
                CorrelationId = "risk-correlation-test-001"
            },
            CancellationToken.None);

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!
                .GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should()
                .Be("risk-correlation-test-001");


        }

        [Fact]
        public async Task CheckRiskOrderAsync_WhenGrpcReturnsRejected_ShouldReeturnRejectedCheckResult()
        {
            var riskDecisionId = Guid.NewGuid();

            var grpcResponse = new RiskService.Grpc.CheckOrderRiskResponse
            {
                Approved = false,
                ReasonCode = "MAX_ORDER_SIZE_EXCEEDED",
                Reason = "Quantity 1000001 exceeds maximum order size 1000000.",
                RiskDecisionId = riskDecisionId.ToString()
            };

            var grpcClient = new Mock<RiskService.Grpc.Risk.RiskClient>();

            grpcClient
            .Setup(x => x.CheckOrderRiskAsync(
                It.IsAny<RiskService.Grpc.CheckOrderRiskRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcRiskClient(grpcClient.Object);

            var result = await client.CheckOrderRiskAsync(
                new SubmitOrder
                {
                    OrderId = Guid.NewGuid(),
                    ClientId = "client-001",
                    Symbol = "EURUSD",
                    Side = OrderSide.Buy,
                    Quantity = 1_000_001m,
                    OrderType = OrderType.Market,
                    CorrelationId = "risk-rejected-test-001"
                },
                CancellationToken.None);

            result.Approved.Should().BeFalse();
            result.ReasonCode.Should().Be("MAX_ORDER_SIZE_EXCEEDED");
            result.Reason.Should().Be("Quantity 1000001 exceeds maximum order size 1000000.");
            result.RiskDecisionId.Should().Be(riskDecisionId);

            grpcClient.Verify(x => x.CheckOrderRiskAsync(
                It.IsAny<RiskService.Grpc.CheckOrderRiskRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        }

        private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        {
            return new AsyncUnaryCall<T>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }
    }
}
