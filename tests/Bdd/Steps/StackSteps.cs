using AspireQuotesPoc.Specs.Support;
using Reqnroll;

namespace AspireQuotesPoc.Specs.Steps;

/// <summary>Shared preconditions that describe the harness rather than a business action.</summary>
[Binding]
public sealed class StackSteps
{
    [Given("the distributed application is running")]
    public void GivenTheDistributedApplicationIsRunning()
    {
        // The stack actually starts once per run in AspireStack's BeforeTestRun hook. This
        // step documents the precondition every scenario shares; reading Application also
        // fails fast with a clear message if the hook never ran.
        _ = AspireStack.Application;
    }
}
