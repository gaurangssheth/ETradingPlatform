using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeCaptureService.Pricing;
using TradeCaptureService.Services;
using TradingApp.Contracts.Shared;

namespace TradeCaptureService_tests
{
    public sealed class ExecutionPriceCalculatorTests
    {
        [Fact]
        public void Calculate_WhenOrderSideIsBuy_ShouldReturnAsk()
        {
            var calculator = new ExecutionPriceCalculator();

            var quote = new PriceQuote
            {
                Symbol = "EURUSD",
                Bid = 1.0849m,
                Ask = 1.0851m,
                Mid = 1.0850m
            };

            var result = calculator.Calculate(OrderSide.Buy, quote);

            result.Should().Be(1.0851m);
        }

        [Fact]
        public void Calculate_WhenOrderSideIsSell_ShouldReturnBid()
        {
            var calculator = new ExecutionPriceCalculator();

            var quote = new PriceQuote
            {
                Symbol = "EURUSD",
                Bid = 1.0849m,
                Ask = 1.0851m,
                Mid = 1.0850m
            };

            var result = calculator.Calculate(OrderSide.Sell, quote);

            result.Should().Be(1.0849m);
        }
    }
}
