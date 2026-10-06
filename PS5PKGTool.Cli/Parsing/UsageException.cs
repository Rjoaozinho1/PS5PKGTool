namespace PS5PKGTool.Cli.Parsing;

/// <summary>Bad command-line usage (exit code 2). <see cref="Command"/> selects the help hint.</summary>
internal sealed class UsageException(string message, string? command = null) : Exception(message)
{
    public string? Command { get; } = command;
}
