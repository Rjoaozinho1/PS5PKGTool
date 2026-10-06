using PS5PKGTool.Cli.Output;
using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli.Commands;

/// <summary>Streams and progress shared by every command. <see cref="Error"/> is synchronized.</summary>
internal sealed record CommandContext(TextWriter Out, TextWriter Error, ProgressReporter Progress);

internal interface ICommand
{
    string Name { get; }

    /// <summary>One line for the command list, without a trailing period.</summary>
    string Summary { get; }

    /// <summary>Usage line shown at the top of the command's help.</summary>
    string Usage { get; }

    IReadOnlyList<OptionSpec> Options { get; }

    Task<int> RunAsync(ParsedArgs args, CommandContext context, CancellationToken cancellationToken);
}
