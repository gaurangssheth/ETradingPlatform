using Microsoft.AspNetCore.SignalR;

namespace TradingGateway.Api.Hubs
{
    public sealed class MarketDataHub : Hub
    {
        private readonly ILogger<MarketDataHub> logger;

        public MarketDataHub(ILogger<MarketDataHub> logger)
        {
            this.logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            logger.LogInformation("SignalR client connected. ConnectionId={ConnectionId}", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            logger.LogInformation(exception, "SignalR client disconnected. ConnectionId={ConnectionId}",
                Context.ConnectionId);

            return base.OnDisconnectedAsync(exception);
        }
    }
}
