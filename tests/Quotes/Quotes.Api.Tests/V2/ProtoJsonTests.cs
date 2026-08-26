using System.Text.Json;
using Quotes.Api.V2.Contracts;
using Quotes.Api.V2.Proto;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// v2 has no DTOs: request and response bodies go through Google.Protobuf's JSON mapping
/// directly. The two behaviors the byte-parity guarantee depends on live here — default
/// values must be emitted (proto3 would otherwise drop <c>page:1</c> from every first-page
/// response) and unknown JSON members must be ignored (the same posture System.Text.Json
/// gives the v0/v1 DTO binding). Property order is asserted too: JSON-PB writes fields in
/// field-number order, which is what keeps the v2 body shaped exactly like v0/v1's.
/// </summary>
public class ProtoJsonTests
{
    [Fact]
    public void Format_emits_default_valued_scalars_in_field_number_order()
    {
        // Everything except page sits at its proto default; parity with v0/v1 requires
        // every property to appear anyway.
        var message = new ListQuotesResponse { Page = 1 };

        var json = ProtoJson.Format(message);

        using var document = JsonDocument.Parse(json);
        document.RootElement.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["items", "page", "pageSize", "totalItems", "totalPages"]);
        document.RootElement.GetProperty("page").GetInt32().ShouldBe(1);
        document.RootElement.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Parse_ignores_unknown_json_members()
    {
        var parsed = ProtoJson.Parse<CreateQuoteRequest>(
            """{"text":"Talk is cheap. Show me the code.","author":"Linus Torvalds","futureField":42}""");

        parsed.Text.ShouldBe("Talk is cheap. Show me the code.");
        parsed.Author.ShouldBe("Linus Torvalds");
    }

    [Theory]
    [InlineData("""{"page":2,"pageSize":5}""", 2, 5)]
    [InlineData("""{"page":3,"page_size":7}""", 3, 7)]
    public void Parse_accepts_both_camel_case_and_snake_case_member_names(string json, int expectedPage, int expectedPageSize)
    {
        // JSON-PB accepts both spellings by specification; clients and gateways that send
        // proto-native names must keep working.
        var parsed = ProtoJson.Parse<ListQuotesRequest>(json);

        parsed.Page.ShouldBe(expectedPage);
        parsed.PageSize.ShouldBe(expectedPageSize);
    }
}
