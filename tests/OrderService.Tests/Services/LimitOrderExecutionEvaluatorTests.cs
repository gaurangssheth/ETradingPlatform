using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NServiceBus.Testing;
using OrderService.Handlers;
using OrderService.Pricing;
using OrderService.Sagas;
using OrderService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Shared;

namespace OrderService.Tests.Services
{
    public class LimitOrderExecutionEvaluatorTests
    {
        [Theory]
        [InlineData(OrderSide.Buy, 1.0848, 1.0850, 1.0850, true)]
        [InlineData(OrderSide.Buy, 1.0848, 1.0850, 1.0849, false)]
        [InlineData(OrderSide.Sell, 1.0848, 1.0850, 1.0848, true)]
        [InlineData(OrderSide.Sell, 1.0848, 1.0850, 1.0849, false)]
        public void CanExecute_ShouldReturnExpectedResult(
            OrderSide side,
            double bid,
            double ask,
            double limitPrice,
            bool expected)
        {
            var evaluator = new LimitOrderExecutionEvaluator();

            var result =
                evaluator.CanExecute(
                    side,
                    (decimal)limitPrice,
                    (decimal)bid,
                    (decimal)ask);

            result.Should().Be(expected);
        }

        [Fact]
        public void CanExecute_WhenSideIsUnsupported_ShouldThrow()
        {
            var evaluator = new LimitOrderExecutionEvaluator();

            var unsupportedSide =
                (OrderSide)999;

            Action action = () =>
                evaluator.CanExecute(
                    unsupportedSide,
                    1.0850m,
                    1.0848m,
                    1.0850m);

            action.Should()
                .Throw<ArgumentOutOfRangeException>()
                .WithParameterName("side");
        }
    }
}
