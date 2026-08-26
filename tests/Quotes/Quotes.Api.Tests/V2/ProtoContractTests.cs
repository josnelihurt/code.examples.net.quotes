using Google.Api;
using Google.Protobuf.Reflection;
using Quotes.Api.V2.Contracts;

namespace Quotes.Api.Tests.V2;

/// <summary>
/// <c>quotes_v2.proto</c> is v2's contract of record: the HTTP adapter, the OpenAPI document
/// and the route table all claim to mirror it. This suite checks that claim against the
/// compiled descriptor Grpc.Tools ships — no host, no routes — so an annotation typo or a
/// renamed JSON field fails here before it fails on the wire. The JSON names and field
/// numbers are what the byte-parity guarantee with v0/v1 is made of: proto3 serializes
/// fields in field-number order, so 1..5 in declaration order is what keeps the list
/// response's property order identical to the DTO transports.
/// </summary>
public class ProtoContractTests
{
    private static ServiceDescriptor Service => QuoteService.Descriptor;

    private static HttpRule HttpRuleOf(MethodDescriptor method)
    {
        var rule = method.GetOptions().GetExtension(AnnotationsExtensions.Http);
        rule.ShouldNotBeNull($"method {method.Name} must carry a google.api.http rule");
        return rule;
    }

    [Fact]
    public void The_service_exposes_exactly_the_four_annotated_methods()
    {
        Service.FullName.ShouldBe("quotes.v2.QuoteService");
        Service.Methods.Select(method => method.Name)
            .ShouldBe(["GetRandomQuote", "ListQuotes", "GetQuoteById", "CreateQuote"]);
    }

    [Theory]
    [InlineData("GetRandomQuote", "GET", "/api/v2/quotes/random", "")]
    [InlineData("ListQuotes", "GET", "/api/v2/quotes", "")]
    [InlineData("GetQuoteById", "GET", "/api/v2/quotes/{id}", "")]
    [InlineData("CreateQuote", "POST", "/api/v2/quotes", "*")]
    public void Each_method_carries_the_expected_http_rule(string methodName, string verb, string pattern, string body)
    {
        var method = Service.Methods.Single(m => m.Name == methodName);
        var rule = HttpRuleOf(method);

        rule.PatternCase.ShouldBe(verb switch
        {
            "GET" => HttpRule.PatternOneofCase.Get,
            "POST" => HttpRule.PatternOneofCase.Post,
            _ => throw new InvalidOperationException($"unexpected verb {verb}")
        });
        (verb == "GET" ? rule.Get : rule.Post).ShouldBe(pattern);
        rule.Body.ShouldBe(body);
        // The HTTP half of v2 reads no additional bindings; one rule per method.
        rule.AdditionalBindings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(nameof(Quote), "id,text,author")]
    [InlineData(nameof(ListQuotesResponse), "items,page,pageSize,totalItems,totalPages")]
    public void Message_field_json_names_match_the_wire_shape(string messageName, string expectedJsonNames)
    {
        var descriptor = Service.File.MessageTypes.Single(m => m.Name == messageName);

        descriptor.Fields.InFieldNumberOrder()
            .Select(field => field.JsonName)
            .ShouldBe(expectedJsonNames.Split(','));
    }

    [Fact]
    public void ListQuotesResponse_field_numbers_are_one_through_five_in_wire_order()
    {
        ListQuotesResponse.Descriptor.Fields.InFieldNumberOrder()
            .Select(field => field.FieldNumber)
            .ShouldBe([1, 2, 3, 4, 5]);
    }
}
