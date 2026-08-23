using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>Vocabulary for the platform surfaces each API publishes about itself.</summary>
[Binding]
public sealed class DocumentationSteps(ApiWorld world)
{
    [When("I open {string} on the {string} service")]
    public async Task WhenIOpenOnTheService(string path, string service)
    {
        // OpenAPI and Scalar are addressed on the services themselves: the gateway only
        // routes the /api prefixes (the curl smoke test hit AUTH_URL/QUOTES_URL too).
        var response = await AspireStack.CreateServiceClient(service).GetAsync(path);
        await world.RecordAsync(response);
    }
}
