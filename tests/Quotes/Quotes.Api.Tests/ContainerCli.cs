using System.Diagnostics;

namespace Quotes.Api.Tests;

/// <summary>
/// Minimal shell-out for the health-degradation test: pause/unpause the backing container.
/// Discovery is operation-based, not version-based: CI runners install podman even though
/// their containers run under docker, so a CLI is chosen by it actually knowing the
/// container, tried per operation.
/// </summary>
internal static class ContainerCli
{
    private static readonly string[] _candidates = ["docker", "podman"];

    public static void Pause(string containerId) => RunAgainstContainer("pause", containerId);

    public static void Unpause(string containerId) => RunAgainstContainer("unpause", containerId);

    private static void RunAgainstContainer(string verb, string containerId)
    {
        Exception? last = null;
        foreach (var cli in _candidates)
        {
            try
            {
                Run(cli, $"{verb} {containerId}");
                return;
            }
            catch (Exception ex)
            {
                // This daemon does not know the container (or is not installed); try the
                // next candidate before giving up.
                last = ex;
            }
        }

        throw new InvalidOperationException($"No container runtime could '{verb}' {containerId}.", last);
    }

    private static void Run(string cli, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = cli,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException($"Could not start {cli}.");

        process.WaitForExit(TimeSpan.FromSeconds(30));
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{cli} {arguments} failed with {process.ExitCode}: {process.StandardError.ReadToEnd()}");
        }
    }
}
