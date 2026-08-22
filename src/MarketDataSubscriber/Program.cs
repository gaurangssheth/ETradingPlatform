using NetMQ;
using NetMQ.Sockets;
using System.Text.Json;
using TradingApp.MarketData.Contracts;
Console.WriteLine("Starting MarketDataSubscriber...");

using var subscriberSocket = new SubscriberSocket();
subscriberSocket.Connect("tcp://localhost:5555");

subscriberSocket.Subscribe(string.Empty);

Console.WriteLine("Subscriber connected to tcp://localhost:5555");
Console.WriteLine("Waiting for messages...");

while (true)
{
    var topic = subscriberSocket.ReceiveFrameString();
    var payload = subscriberSocket.ReceiveFrameString();

    var tick = JsonSerializer.Deserialize<PriceTick>(payload);

    if (tick is null)
    {
        Console.WriteLine("Could not deserialize PriceTick.");
        continue;
    }

    Console.WriteLine($"Topic: {topic}");
    Console.WriteLine($"Symbol: {tick.Symbol}");
    Console.WriteLine($"Bid: {tick.Bid}");
    Console.WriteLine($"Ask: {tick.Ask}");
    Console.WriteLine($"Timestamp: {tick.Timestamp}");
    Console.WriteLine();
}

Console.ReadLine();