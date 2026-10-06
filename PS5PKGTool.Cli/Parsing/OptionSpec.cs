namespace PS5PKGTool.Cli.Parsing;

internal enum OptionKind
{
    Flag,
    Value
}

/// <summary>Declares one command-line option. <see cref="Name"/> is the long name without dashes.</summary>
internal sealed record OptionSpec(string Name, OptionKind Kind, string Help)
{
    /// <summary>Single-letter short form without the dash, for example "o" for -o.</summary>
    public string? Alias { get; init; }

    /// <summary>Placeholder shown in help for free-form values, for example "path".</summary>
    public string? ValueName { get; init; }

    /// <summary>Accepted values, matched case-insensitively and stored exactly as listed here.</summary>
    public IReadOnlyList<string>? Allowed { get; init; }

    /// <summary>Accepted whole-number range, inclusive.</summary>
    public (int Min, int Max)? Range { get; init; }
}
