using Quotes.Api.V0;
using Quotes.Api.V1;
using Quotes.Api.V2;
using Quotes.Api.V3;

namespace Quotes.Api.ApiModules;

/// <summary>
/// The host's API versions in stack order. This file is the one place they are listed —
/// explicit and greppable rather than discovered by reflection, which is easy to break
/// silently (a rename, a trimming pass) and hides the wiring from a plain-text search.
/// Adding a version means adding a module and one line here.
/// </summary>
internal static class ApiModuleRegistry
{
    internal static readonly IReadOnlyList<IApiModule> Modules =
    [
        new V0ApiModule(),
        new V1ApiModule(),
        new V2ApiModule(),
        new V3ApiModule(),
    ];
}
