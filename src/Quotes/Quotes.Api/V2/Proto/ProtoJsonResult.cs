namespace Quotes.Api.V2.Proto;

/// <summary>
/// Writes a response already serialized by <see cref="ProtoJson"/> — JSON-PB bytes, not
/// System.Text.Json — so proto messages can be served without a round trip through CLR
/// reflection-based serialization, while keeping the media type and Location semantics the
/// other transports produce.
/// </summary>
internal sealed class ProtoJsonResult(int statusCode, string? location, string json) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;
        if (location is not null)
        {
            httpContext.Response.Headers.Location = location;
        }

        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsync(json, httpContext.RequestAborted);
    }
}
