using System.Net;
using System.Text.Json;
using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>Vocabulary for reading and publishing quotes through the gateway.</summary>
[Binding]
public sealed class QuoteSteps(ApiWorld world)
{
    [When("I request a random quote from {string}")]
    public async Task WhenIRequestARandomQuoteFrom(string version)
    {
        var response = await world.Client.GetAsync($"/api/{version}/quotes/random");
        await world.RecordAsync(response);
    }

    [When("I request the quote with id {string} from {string}")]
    public async Task WhenIRequestTheQuoteWithIdFrom(string id, string version)
    {
        var response = await world.Client.GetAsync($"/api/{version}/quotes/{id}");
        await world.RecordAsync(response);
    }

    [When("I request the quote I published from {string}")]
    public async Task WhenIRequestTheQuoteIPublishedFrom(string version)
    {
        var id = world.LastCreatedId.ShouldNotBeNull("a publish step must run first");
        var response = await world.Client.GetAsync($"/api/{version}/quotes/{id}");
        await world.RecordAsync(response);
    }

    [When("I list quotes from {string}")]
    public async Task WhenIListQuotesFrom(string version)
    {
        var response = await world.Client.GetAsync($"/api/{version}/quotes");
        await world.RecordAsync(response);
    }

    [When("I list the first page from {string}")]
    public async Task WhenIListTheFirstPageFrom(string version)
    {
        var response = await world.Client.GetAsync($"/api/{version}/quotes?page=1&pageSize=3");
        await world.RecordAsync(response);
    }

    [When("I list page {int} with size {int} from {string}")]
    public async Task WhenIListPageWithSizeFrom(int page, int pageSize, string version)
    {
        var response = await world.Client.GetAsync($"/api/{version}/quotes?page={page}&pageSize={pageSize}");
        await world.RecordAsync(response);
    }

    [When("I publish a quote with unique text attributed to {string}")]
    public Task WhenIPublishAQuoteWithUniqueTextAttributedTo(string author) =>
        PublishAsync(world.UniqueText, author);

    [Given("I have published a quote with unique text attributed to {string}")]
    public async Task GivenIHavePublishedAQuoteWithUniqueTextAttributedTo(string author)
    {
        await PublishAsync(world.UniqueText, author);
        world.LastResponse.ShouldNotBeNull().StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [When("I publish the same text with the final period replaced by an exclamation mark")]
    public Task WhenIPublishTheSameTextWithTheFinalPeriodReplacedByAnExclamationMark() =>
        PublishAsync($"{world.UniqueText.TrimEnd('.')}!", "Somebody Else");

    [When("I publish a quote with the text {string}")]
    public Task WhenIPublishAQuoteWithTheText(string text) => PublishAsync(text, "Specification Suite");

    [When("I publish a quote with unique text attributed to {string} through the {string} transport")]
    public Task WhenIPublishAQuoteWithUniqueTextAttributedToThroughTheTransport(string author, string version) =>
        PublishAsync(world.UniqueText, author, version);

    [When("I publish a quote with the text {string} through the {string} transport")]
    public Task WhenIPublishAQuoteWithTheTextThroughTheTransport(string text, string version) =>
        PublishAsync(text, "Specification Suite", version);

    // The v1 default keeps every existing scenario unchanged; the versioned overloads serve
    // the transports whose create contract differs (v3 answers 200, not 201).
    private async Task PublishAsync(string text, string author, string version = "v1")
    {
        var response = await world.Client.PostAsync(
            $"/api/{version}/quotes",
            ApiWorld.JsonBody(JsonSerializer.Serialize(new { text, author })));
        await world.RecordAsync(response);

        // 201 (v0/v1/v2) and 200 (v3) both return the created quote in the body.
        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
        {
            world.LastCreatedId = world.LastBody?.GetProperty("id").GetString();
        }
    }
}
