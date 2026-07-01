using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using OrderService.Handlers;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using OrderService.Infrastructure.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService_tests.Handlers
{
    public class SubmitOrderHandlerTests
    {
        [Fact]
        public async Task Handle_WhenSubmitOrderReceived_ShouldSaveOrderAndPublishOrderAccepted()
        {
            await using var connection = new SqliteConnection("Datasource=:memory:");
            await connection.OpenAsync();

            await using var dbContext = CreateDbContext(connection);
            await dbContext.Database.EnsureCreatedAsync();

            var handler = CreateHandler(dbContext);
            var messageContext = new TestableMessageHandlerContext();

            var orderId = Guid.NewGuid();

            var command = new SubmitOrder
            {
                OrderId = orderId,
                ClientId = "client-001",
                Symbol = "EURUSD",
                Side = OrderSide.Buy,
                OrderType = OrderType.Market,
                Quantity = 100000m,
                CorrelationId = "submit-order-handler-test-001"
            };

            await handler.Handle(command, messageContext);

            var order = await dbContext.Orders.SingleAsync(x => x.Id == command.OrderId);

            order.Id.Should().Be(orderId);
            order.ClientId.Should().Be("client-001");
            order.Symbol.Should().Be("EURUSD");
            order.Side.Should().Be(OrderSide.Buy);
            order.Quantity.Should().Be(100000m);
            order.OrderType.Should().Be(OrderType.Market);
            order.Status.Should().Be("Accepted");
            order.CorrelationId.Should().Be("submit-order-handler-test-001");
            order.CreatedAt.Should().NotBe(default);
            order.AcceptedAt.Should().NotBeNull();

            messageContext.PublishedMessages.Should().ContainSingle();

            var published = messageContext.PublishedMessages.Single().Message
                .Should()
                .BeOfType<OrderAccepted>()
                .Subject;


            published.OrderId.Should().Be(orderId);
            published.ClientId.Should().Be("client-001");
            published.Symbol.Should().Be("EURUSD");
            published.Side.Should().Be(OrderSide.Buy);
            published.OrderType.Should().Be(OrderType.Market);
            published.Quantity.Should().Be(100000m);
            published.CorrelationId.Should().Be("submit-order-handler-test-001");
            published.AcceptedAt.Should().NotBe(default);
        }

        private OrderDbContext CreateDbContext(SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<OrderDbContext>()
                .UseSqlite(connection)
                .Options;

            return new OrderDbContext(options);
        }

        private SubmitOrderHandler CreateHandler(
            OrderDbContext dbContext)
        {
            var unitOfWork = new EfUnitOfWork(
                dbContext,
                new OrderRepository(dbContext, NullLogger<OrderRepository>.Instance));

            return new SubmitOrderHandler(
                unitOfWork,
                NullLogger<SubmitOrderHandler>.Instance);
        }
    }
}
