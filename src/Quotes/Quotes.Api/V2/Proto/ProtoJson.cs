using Google.Protobuf;

namespace Quotes.Api.V2.Proto;

/// <summary>
/// JSON-PB plumbing for the v2 transport: the adapter parses request bodies and writes
/// response bodies through Google.Protobuf's own JSON mapping so the messages Grpc.Tools
/// generates from the contract are the only DTOs this version needs.
/// </summary>
internal static class ProtoJson
{
    /// <summary>
    /// Parses like a lenient gateway: unknown fields are ignored rather than rejected, the
    /// same posture the minimal-API JSON binding gives v0/v1 (STJ ignores unmapped members).
    /// </summary>
    private static readonly JsonParser _parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    /// <summary>
    /// Formats with default values included (<c>FormatDefaultValues</c>) so a first page
    /// answers <c>{"page":1,...}</c> exactly like v0/v1 do — proto3's implicit presence would
    /// otherwise omit fields sitting at their default, and byte parity forbids that.
    /// </summary>
    private static readonly JsonFormatter _formatter = new(JsonFormatter.Settings.Default.WithFormatDefaultValues(true));

    internal static T Parse<T>(string json) where T : IMessage<T>, new() => _parser.Parse<T>(json);

    internal static string Format(IMessage message) => _formatter.Format(message);
}
