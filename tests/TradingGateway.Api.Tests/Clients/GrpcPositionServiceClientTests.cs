using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PositionService.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Shared.Messaging.Correlation;
using TradingGateway.Api.Clients;

namespace TradingGateway.Api.Tests.Clients
{
    public class GrpcPositionServiceClientTests
    {
        [Fact]
        public async Task GetOpenPositionsByClientIdAsync_ShouldCallGrpcClientAndReturnPositions()
        {
            var grpcResponse = new GetOpenPositionsByClientIdResponse();

            grpcResponse.Positions.Add(
                new PositionSummary
                {
                    InstrumentId = "11111111-1111-1111-1111-111111111111",
                    Symbol = "EURUSD",
                    AssetClass = "Fx",
                    NetQuantity = "100000",
                    AveragePrice = "1.0850",
                    PnlCurrency = "USD",
                    RealisedPnl = "25",
                    UnrealisedPnl = "75",
                    TotalPnl = "100"
                });

            var asyncUnaryCall = CreateAsyncUnaryCall(grpcResponse);

            var grpcClientMock = new Mock<PositionService.Grpc.PositionService.PositionServiceClient>();

            grpcClientMock.Setup(client => client.GetOpenPositionsByClientIdAsync(
                It.Is<GetOpenPositionsByClientIdRequest>(request => request.ClientId == "client-001"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
                .Returns(asyncUnaryCall);

            var client = new GrpcPositionServiceClient(grpcClientMock.Object, NullLogger<GrpcPositionServiceClient>.Instance);

            var result = await client.GetOpenPositionsByClientIdAsync("client-001");

            result.Should().ContainSingle();

            var position = result.Single();

            position.InstrumentId.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            position.Symbol.Should().Be("EURUSD");
            position.AssetClass.Should().Be("Fx");
            position.NetQuantity.Should().Be(100000m);
            position.AveragePrice.Should().Be(1.0850m);
            position.PnlCurrency.Should().Be("USD");
            position.RealisedPnl.Should().Be(25m);
            position.UnrealisedPnl.Should().Be(75m);
            position.TotalPnl.Should().Be(100m);

            grpcClientMock.Verify(
            x => x.GetOpenPositionsByClientIdAsync(
                It.Is<GetOpenPositionsByClientIdRequest>(request => request.ClientId == "client-001"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task GetOpenPositionsByClientIdAsync_WhenCorrelationIdProvided_ShouldSendCorrelationIdInGrpcMetadata()
        {
            var grpcResponse = new GetOpenPositionsByClientIdResponse();

            Metadata? capturedHeaders = null;

            var grpcClientMock = new Mock<PositionService.Grpc.PositionService.PositionServiceClient>();

            grpcClientMock.Setup(client => client.GetOpenPositionsByClientIdAsync(
                It.Is<GetOpenPositionsByClientIdRequest>(request => request.ClientId == "client-001"),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
                .Callback<GetOpenPositionsByClientIdRequest, Metadata, DateTime?, CancellationToken>(
                    (_, headers, _, _) => capturedHeaders = headers)
                .Returns(CreateAsyncUnaryCall(grpcResponse));

            var client = new GrpcPositionServiceClient(grpcClientMock.Object, NullLogger<GrpcPositionServiceClient>.Instance);

            await client.GetOpenPositionsByClientIdAsync(
                "client-001",
                "positions-correlation-test-001",
                CancellationToken.None);

            capturedHeaders.Should().NotBeNull();

            capturedHeaders!.GetValue(GrpcCorrelationConstants.MetadataKey)
                .Should().Be("positions-correlation-test-001");

        }

        private static AsyncUnaryCall<T>
        CreateAsyncUnaryCall<T>(
            T response)
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
