using System.Diagnostics;

namespace AspireQuotesPoc.Specs.Support;

/// <summary>
/// Minimal shell-out to the container runtime for the health-readiness journey: DCP
/// manages the stack's containers, but pausing one for a degradation test needs the
/// runtime CLI. The CLI is resolved by whoever actually runs the stack's container —
/// a machine can have both daemons (docker AND podman), so probing a version alone
/// would pick the wrong one.
/// </summary>
internal static class ContainerRuntime
{
    private static readonly string[] _candidates = ["docker", "podman"];
    private static string? _cli;

    private static string Cli => _cli ?? throw new InvalidOperationException(
        "Resolve the runtime via FindByPublishedPort before pausing or unpausing.");

    /// <summary>
    /// The stack's PostgreSQL container: the postgres-* container created after the suite
    /// booted. DCP publishes no host port for it, so creation time is the discriminator —
    /// leftovers of crashed previous runs predate the suite by definition.
    /// </summary>
    public static string FindPostgresContainer(DateTimeOffset createdAfterUtc)
    {
        foreach (var cli in _candidates)
        {
            try
            {
                var names = Run(cli, "ps --format {{.Names}}")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(name => name.StartsWith("postgres-", StringComparison.Ordinal))
                    .ToList();

                var ours = names
                    .Where(name =>
                    {
                        var created = ParseCreated(cli, name);
                        return created > createdAfterUtc;
                    })
                    .ToList();

                if (ours.Count == 1)
                {
                    _cli = cli;
                    return ours[0];
                }

                if (ours.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"Multiple postgres-* containers were created after the suite booted: [{string.Join(", ", ours)}].");
                }

                // This daemon does not see the container; try the next candidate.
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // CLI missing or no daemon — try the next candidate.
            }
        }

        throw new InvalidOperationException(
            "No container runtime reports a postgres container created during this suite; the stack's database is not running.");
    }

    private static DateTimeOffset ParseCreated(string cli, string container)
    {
        var created = Run(cli, $"inspect --format {{{{.Created}}}} {container}").Trim();

        // docker emits RFC 3339; podman emits "2026-08-23 19:28:38.538289568 -0700 PDT" —
        // normalize the podman shape into something DateTimeOffset accepts: drop the zone
        // abbreviation, give the offset a colon, cap the fraction at seven digits.
        created = System.Text.RegularExpressions.Regex.Replace(created, @"\s+[A-Za-z]{2,5}$", "");
        created = System.Text.RegularExpressions.Regex.Replace(created, @"([+-]\d{2})(\d{2})$", "$1:$2");
        created = System.Text.RegularExpressions.Regex.Replace(created, @"(\.\d{7})\d+", "$1");

        return DateTimeOffset.TryParse(created, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Could not parse creation time '{created}' of container {container}.");
    }

    /// <summary>Diagnostic: every running container as "name|ports", for failure messages.</summary>
    public static string DescribeRunningContainers()
    {
        foreach (var cli in _candidates)
        {
            try
            {
                return string.Join("; ", Run(cli, "ps --format {{.Names}}|{{.Ports}}")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                // CLI missing or no daemon — try the next candidate.
            }
        }

        return "(no container runtime available)";
    }

    public static void Stop(string container) => Run(Cli, $"stop -t 1 {container}");

    public static void Start(string container) => Run(Cli, $"start {container}");

    private static string Run(string cli, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = cli,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException($"Could not start {cli}.");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(30));
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{cli} {arguments} failed with {process.ExitCode}: {process.StandardError.ReadToEnd()}");
        }

        return output;
    }
}
