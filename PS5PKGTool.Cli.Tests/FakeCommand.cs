using PS5PKGTool.Cli.Commands;
using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli.Tests;

internal sealed class FakeCommand(Func<ParsedArgs, CommandContext, CancellationToken, Task<int>> run) : ICommand
{
    public string Name => "fake";
    public string Summary => "Test command";
    public string Usage => "ps5pkg fake [--flag]";
    public IReadOnlyList<OptionSpec> Options { get; } = [new("flag", OptionKind.Flag, "A test flag")];

    public Task<int> RunAsync(ParsedArgs args, CommandContext context, CancellationToken cancellationToken) =>
        run(args, context, cancellationToken);
}
