namespace Quotes.Api.Tests;

/// <summary>
/// Groups every test class that boots a real host through <see cref="QuoteApiFactory"/>.
/// </summary>
/// <remarks>
/// Collections are xUnit's unit of parallelism, so naming one here makes these classes run one
/// after another. They must: <c>Program</c> assigns Serilog's bootstrap logger to the static
/// <c>Log.Logger</c>, and two hosts starting at once race to freeze it — the second loses with
/// "The logger is already frozen". Each class still gets its own factory, and therefore its own
/// migrated database on the shared PostgreSQL container, so serializing them costs isolation
/// nothing.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class WebHostCollection
{
    public const string Name = "web-host";
}
