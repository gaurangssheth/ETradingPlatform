using System.Security.Claims;
using System.Text;
using Grpc.Core;

namespace PricingService.Tests;

public sealed class TestServerCallContext : ServerCallContext
{
    private readonly Metadata requestHeaders;
    private readonly CancellationToken cancellationToken = CancellationToken.None;
    private readonly Metadata responseTrailers = new();
    private Status status;
    private WriteOptions? writeOptions;

    public static ServerCallContext Create()
    {
        return new TestServerCallContext();
    }

    public static ServerCallContext Create(Metadata requestHeaders)
    {
        return new TestServerCallContext(requestHeaders);
    }

    private TestServerCallContext(Metadata? requestHeaders = null)
    {
        this.requestHeaders = requestHeaders ?? new Metadata();
    }

    protected override string MethodCore => "GetPrice";

    protected override string HostCore => "localhost";

    protected override string PeerCore => "localhost";

    protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);

    protected override Metadata RequestHeadersCore => requestHeaders;

    protected override CancellationToken CancellationTokenCore => cancellationToken;

    protected override Metadata ResponseTrailersCore => responseTrailers;

    protected override Status StatusCore
    {
        get => status;
        set => status = value;
    }

    protected override WriteOptions? WriteOptionsCore
    {
        get => writeOptions;
        set => writeOptions = value;
    }

    protected override AuthContext AuthContextCore =>
        new("anonymous", new Dictionary<string, List<AuthProperty>>());

    protected override ContextPropagationToken CreatePropagationTokenCore(
        ContextPropagationOptions? options)
    {
        throw new NotImplementedException();
    }

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
    {
        return Task.CompletedTask;
    }
}