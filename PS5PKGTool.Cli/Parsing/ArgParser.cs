using System.Globalization;

namespace PS5PKGTool.Cli.Parsing;

internal static class ArgParser
{
    /// <summary>
    /// Parses the arguments that follow a command name. Supported forms: <c>--name value</c>,
    /// <c>--name=value</c>, <c>-o value</c>, <c>--flag</c>, and <c>--</c> to end options. A value that
    /// starts with '-' must use the <c>--name=value</c> form.
    /// </summary>
    public static ParsedArgs Parse(IReadOnlyList<string> args, IReadOnlyList<OptionSpec> specs, string command)
    {
        var parsed = new ParsedArgs();
        bool positionalOnly = false;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (positionalOnly || !IsOptionToken(arg))
            {
                parsed.Positionals.Add(arg);
                continue;
            }
            if (arg == "--")
            {
                positionalOnly = true;
                continue;
            }

            bool isLong = arg.StartsWith("--", StringComparison.Ordinal);
            string name = isLong ? arg[2..] : arg[1..];
            string? inlineValue = null;
            int equals = isLong ? name.IndexOf('=') : -1;
            if (equals >= 0)
            {
                inlineValue = name[(equals + 1)..];
                name = name[..equals];
            }

            OptionSpec spec = specs.FirstOrDefault(candidate => isLong ? candidate.Name == name : candidate.Alias == name)
                ?? throw new UsageException($"unknown option '{(isLong ? "--" : "-") + name}'", command);
            string display = "--" + spec.Name;

            if (spec.Kind == OptionKind.Flag)
            {
                if (inlineValue is not null)
                    throw new UsageException($"option '{display}' does not take a value", command);
                parsed.Flags.Add(spec.Name);
                continue;
            }

            string value;
            if (inlineValue is not null)
                value = inlineValue;
            else if (i + 1 < args.Count && !IsOptionToken(args[i + 1]))
                value = args[++i];
            else
                throw new UsageException(
                    $"option '{display}' needs a value (use {display}=<value> for values that start with '-')", command);

            if (parsed.Values.ContainsKey(spec.Name))
                throw new UsageException($"option '{display}' was given more than once", command);
            parsed.Values[spec.Name] = Validate(spec, value, display, command);
        }
        return parsed;
    }

    private static bool IsOptionToken(string arg) => arg.Length > 1 && arg[0] == '-';

    private static string Validate(OptionSpec spec, string value, string display, string command)
    {
        if (value.Length == 0)
            throw new UsageException($"option '{display}' needs a value", command);

        if (spec.Allowed is { } allowed)
        {
            return allowed.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
                ?? throw new UsageException(
                    $"invalid value '{value}' for '{display}' (expected {string.Join(", ", allowed)})", command);
        }

        if (spec.Range is { } range)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
                number < range.Min || number > range.Max)
                throw new UsageException($"'{display}' must be a whole number from {range.Min} to {range.Max}", command);
            return number.ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }
}
