using Grpc.Core;

namespace Quotes.Api.V2.Proto;

/// <summary>
/// The smallest <see cref="ServerCallContext"/> the v2 service handlers can run under: the
/// HTTP adapter invokes the generated service methods in-process, and none of the handlers
/// touch gRPC call machinery — they only read the cancellation token. Every other member
/// fails loudly so a future handler that starts relying on real call state surfaces here
/// instead of silently reading nonsense.
/// </summary>
internal sealed class AdapterServerCallContext : ServerCallContext
{
    private readonly string _method;
    private readonly CancellationToken _cancellationToken;

    internal AdapterServerCallContext(string method, CancellationToken cancellationToken)
    {
        _method = method;
        _cancellationToken = cancellationToken;
    }

    protected override string MethodCore => _method;
    protected override string HostCore => "quotes-api";
    protected override string PeerCore => "http-adapter";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata RequestHeadersCore => [];
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override Task WriteResponseHeadersAsyncCore(Metadata headers) =>
        throw new NotSupportedException("The HTTP adapter writes HTTP headers, not gRPC response headers.");

    protected override IDictionary<object, object> UserStateCore => throw new NotSupportedException(
        "The HTTP adapter context carries no user state; native gRPC calls would.");

    protected override AuthContext AuthContextCore => throw new NotSupportedException(
        "Authorization is enforced by the HTTP pipeline before the adapter runs.");

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException("The HTTP adapter does not propagate gRPC call contexts.");
}
