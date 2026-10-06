using System.Globalization;

namespace PS5PKGTool.Cli.Parsing;

internal sealed class ParsedArgs
{
    public List<string> Positionals { get; } = [];
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Flags { get; } = new(StringComparer.Ordinal);

    public bool Has(string name) => Values.ContainsKey(name) || Flags.Contains(name);

    public string? Value(string name) => Values.GetValueOrDefault(name);

    public int? Int(string name) => Values.TryGetValue(name, out string? value)
        ? int.Parse(value, CultureInfo.InvariantCulture)
        : null;
}
