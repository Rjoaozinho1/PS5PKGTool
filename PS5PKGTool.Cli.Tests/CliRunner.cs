using PS5PKGTool.Cli.Commands;

namespace PS5PKGTool.Cli.Tests;

internal sealed record CliResult(int ExitCode, string Out, string Error);

/// <summary>Runs the CLI in-process with captured, "\n"-terminated stdout and stderr (stderr is not a terminal).</summary>
internal static class CliRunner
{
    public static Task<CliResult> RunAsync(params string[] args) => RunAsync(null, CancellationToken.None, args);

    public static async Task<CliResult> RunAsync(IReadOnlyList<ICommand>? commands,
        CancellationToken cancellationToken, params string[] args)
    {
        var stdout = new StringWriter { NewLine = "\n" };
        var stderr = new StringWriter { NewLine = "\n" };
        int exitCode = await CliApp.RunAsync(args, stdout, stderr, isErrorTerminal: false, cancellationToken,
            commands ?? CliApp.DefaultCommands);
        return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
    }
}
