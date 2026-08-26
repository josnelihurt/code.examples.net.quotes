using ErrorOr;
using Grpc.Core;
using Quotes.Api.V2.Proto;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// The v2 service implementation can only report failures by throwing, so every error
/// field makes a trailer round trip through <see cref="GrpcErrorBridge"/> before the HTTP
/// adapter can render the shared problem envelope. These tests pin that round trip: no
/// field may change (code, description, order, ErrorOr type), and an unreadable trailer
/// must fail closed rather than invent an error a client could mistake for the catalog's.
/// </summary>
public class GrpcErrorBridgeTests
{
    private static List<Error> RoundTrip(params Error[] errors)
    {
        try
        {
            throw GrpcErrorBridge.ToRpcException([.. errors]);
        }
        catch (RpcException ex)
        {
            return GrpcErrorBridge.ToErrors(ex);
        }
    }

    [Fact]
    public void A_single_not_found_error_round_trips_field_by_field()
    {
        var decoded = RoundTrip(Error.NotFound("quote.not_found", "Quote not found."));

        var error = decoded.ShouldHaveSingleItem();
        error.Type.ShouldBe(ErrorType.NotFound);
        error.Code.ShouldBe("quote.not_found");
        error.Description.ShouldBe("Quote not found.");
    }

    [Fact]
    public void Multiple_errors_round_trip_in_their_original_order()
    {
        var decoded = RoundTrip(
            Error.Validation("quote.text_too_short", "Quote text must be at least 12 characters."),
            Error.Conflict("quote.duplicate_fingerprint", "A quote with the same meaning already exists."));

        decoded.Count.ShouldBe(2);
        decoded[0].Code.ShouldBe("quote.text_too_short");
        decoded[0].Type.ShouldBe(ErrorType.Validation);
        decoded[1].Code.ShouldBe("quote.duplicate_fingerprint");
        decoded[1].Type.ShouldBe(ErrorType.Conflict);
    }

    [Theory]
    [InlineData(ErrorType.Validation, StatusCode.InvalidArgument)]
    [InlineData(ErrorType.Conflict, StatusCode.AlreadyExists)]
    [InlineData(ErrorType.NotFound, StatusCode.NotFound)]
    [InlineData(ErrorType.Unexpected, StatusCode.Internal)]
    public void Error_types_map_onto_grpc_status_codes_and_back(ErrorType type, StatusCode expectedStatusCode)
    {
        GrpcErrorBridge.ToGrpcStatusCode(type).ShouldBe(expectedStatusCode);

        var error = ErrorFactory(type);
        var decoded = RoundTrip(error);

        decoded.ShouldHaveSingleItem().Type.ShouldBe(type);
        decoded[0].Code.ShouldBe(error.Code);
        decoded[0].Description.ShouldBe(error.Description);
    }

    private static Error ErrorFactory(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => Error.Validation("test.validation", "validation description"),
        ErrorType.Conflict => Error.Conflict("test.conflict", "conflict description"),
        ErrorType.NotFound => Error.NotFound("test.not_found", "not found description"),
        ErrorType.Unexpected => Error.Unexpected("test.unexpected", "unexpected description"),
        _ => throw new InvalidOperationException($"no factory for {errorType}")
    };

    [Fact]
    public void A_malformed_trailer_fails_closed_to_an_unexpected_error()
    {
        // A trailer written by anything other than the encoder — garbage value, missing
        // segments — must never surface as a catalog error; the decoder falls back to
        // error.unknown with the exception's own detail as the description.
        var exception = new RpcException(
            new Status(StatusCode.Internal, "the bridge emitted something unreadable"),
            new Metadata { { GrpcErrorBridge.MetadataKey, "this is not grpcStatusCode|code|description" } });

        var decoded = GrpcErrorBridge.ToErrors(exception);

        var error = decoded.ShouldHaveSingleItem();
        error.Type.ShouldBe(ErrorType.Unexpected);
        error.Code.ShouldBe("error.unknown");
        error.Description.ShouldBe("the bridge emitted something unreadable");
    }

    [Fact]
    public void An_exception_without_trailers_falls_back_to_its_status_code()
    {
        // Native gRPC failures arrive as statuses, not trailers; the decoder must still
        // rebuild an ErrorOr list the HTTP adapter can render.
        var exception = new RpcException(new Status(StatusCode.NotFound, "Quote not found."));

        var decoded = GrpcErrorBridge.ToErrors(exception);

        var error = decoded.ShouldHaveSingleItem();
        error.Type.ShouldBe(ErrorType.NotFound);
        error.Description.ShouldBe("Quote not found.");
    }
}
