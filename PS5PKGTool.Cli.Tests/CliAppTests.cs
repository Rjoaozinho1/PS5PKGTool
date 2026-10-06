using PS5PKGTool.Cli.Commands;
using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli.Tests;

public class CliAppTests
{
    private static IReadOnlyList<ICommand> Fake(Func<ParsedArgs, CommandContext, CancellationToken, Task<int>> run) =>
        [new FakeCommand(run)];

    private static IReadOnlyList<ICommand> Returns(int code) => Fake((_, _, _) => Task.FromResult(code));

    [Fact]
    public async Task Version_prints_the_assembly_version()
    {
        CliResult result = await CliRunner.RunAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ps5pkg 0.1.0\n", result.Out);
    }

    [Fact]
    public async Task No_arguments_prints_usage_to_stderr_and_exits_2()
    {
        CliResult result = await CliRunner.RunAsync();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: ps5pkg <command> [options]", result.Error);
        Assert.Equal(string.Empty, result.Out);
    }

    [Fact]
    public async Task Help_lists_the_commands()
    {
        CliResult result = await CliRunner.RunAsync(Returns(0), CancellationToken.None, "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("fake", result.Out);
        Assert.Contains("Test command", result.Out);
    }

    [Fact]
    public async Task Unknown_command_is_a_usage_error()
    {
        CliResult result = await CliRunner.RunAsync("nope");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: unknown command 'nope'\nrun 'ps5pkg --help'\n", result.Error);
    }

    [Fact]
    public async Task Unknown_option_before_the_command_is_a_usage_error()
    {
        CliResult result = await CliRunner.RunAsync("--nope");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: unknown option '--nope'\nrun 'ps5pkg --help'\n", result.Error);
    }

    [Fact]
    public async Task Command_help_shows_usage_and_options()
    {
        CliResult result = await CliRunner.RunAsync(Returns(9), CancellationToken.None, "fake", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: ps5pkg fake [--flag]", result.Out);
        Assert.Contains("--flag", result.Out);
        Assert.Contains("Global options:", result.Out);
    }

    [Fact]
    public async Task Command_usage_errors_point_to_the_command_help()
    {
        CliResult result = await CliRunner.RunAsync(Returns(0), CancellationToken.None, "fake", "--bogus");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("error: unknown option '--bogus'\nrun 'ps5pkg fake --help'\n", result.Error);
    }

    [Fact]
    public async Task Parsed_options_reach_the_command_and_global_flags_may_come_first()
    {
        IReadOnlyList<ICommand> commands = Fake((args, _, _) => Task.FromResult(args.Has("flag") ? 0 : 5));

        Assert.Equal(0, (await CliRunner.RunAsync(commands, CancellationToken.None, "--quiet", "fake", "--flag")).ExitCode);
        Assert.Equal(5, (await CliRunner.RunAsync(commands, CancellationToken.None, "fake")).ExitCode);
    }

    [Fact]
    public async Task Failures_print_the_message_and_exit_1()
    {
        CliResult result = await CliRunner.RunAsync(Fake((_, _, _) => throw new IOException("disk full")),
            CancellationToken.None, "fake");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("error: disk full\n", result.Error);
    }

    [Fact]
    public async Task Debug_adds_the_stack_trace()
    {
        CliResult result = await CliRunner.RunAsync(Fake((_, _, _) => throw new IOException("disk full")),
            CancellationToken.None, "fake", "--debug");

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("error: disk full\n", result.Error);
        Assert.Contains("System.IO.IOException: disk full", result.Error);
    }

    [Fact]
    public async Task Cancellation_exits_130()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        CliResult result = await CliRunner.RunAsync(
            Fake((_, _, token) => throw new OperationCanceledException(token)), cancellation.Token, "fake");

        Assert.Equal(130, result.ExitCode);
        Assert.Equal("cancelled\n", result.Error);
    }

    [Fact]
    public async Task Cancellation_that_was_not_requested_is_an_ordinary_failure()
    {
        CliResult result = await CliRunner.RunAsync(
            Fake((_, _, _) => throw new OperationCanceledException()), CancellationToken.None, "fake");

        Assert.Equal(1, result.ExitCode);
    }
}
