using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingApp.Contracts.Commands;
using TradingApp.Contracts.Events;
using TradingApp.Contracts.Shared;

namespace TradingApp.Shared.Tests
{
    public class RealMessageContractTests
    {
        [Fact]
        public void RealMessageContracts_ShouldImplementICorrelatedMessage()
        {
            typeof(SubmitOrder).Should().BeAssignableTo<ICorrelatedMessage>();
            typeof(OrderAccepted).Should().BeAssignableTo<ICorrelatedMessage>();
            typeof(TradeCaptured).Should().BeAssignableTo<ICorrelatedMessage>();
            typeof(PositionUpdated).Should().BeAssignableTo<ICorrelatedMessage>();
        }
    }
}
