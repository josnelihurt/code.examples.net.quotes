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

    private async Task PublishAsync(string text, string author)
    {
        var response = await world.Client.PostAsync(
            "/api/v1/quotes",
            ApiWorld.JsonBody(JsonSerializer.Serialize(new { text, author })));
        await world.RecordAsync(response);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            world.LastCreatedId = world.LastBody?.GetProperty("id").GetString();
        }
    }
}
