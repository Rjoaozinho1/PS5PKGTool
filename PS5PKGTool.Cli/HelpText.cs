using System.Text;
using PS5PKGTool.Cli.Commands;
using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli;

internal static class HelpText
{
    public static string General(IReadOnlyList<ICommand> commands)
    {
        var text = new StringBuilder();
        text.AppendLine("ps5pkg - PS5 dump, package and image tool (command-line PS5 PKG Tool)");
        text.AppendLine();
        text.AppendLine("Usage: ps5pkg <command> [options]");
        if (commands.Count > 0)
        {
            int width = commands.Max(command => command.Name.Length) + 3;
            text.AppendLine();
            text.AppendLine("Commands:");
            foreach (ICommand command in commands)
                text.AppendLine("  " + command.Name.PadRight(width) + command.Summary);
        }
        text.AppendLine();
        text.AppendLine("Global options:");
        text.AppendLine("  -h, --help   Show help; 'ps5pkg <command> --help' shows a command's options");
        text.AppendLine("  --version    Show the version");
        text.AppendLine("  --quiet      Do not show progress");
        text.AppendLine("  --debug      Show stack traces on errors");
        return text.ToString();
    }

    public static string For(ICommand command, IReadOnlyList<OptionSpec> globalOptions)
    {
        var text = new StringBuilder();
        text.AppendLine("Usage: " + command.Usage);
        text.AppendLine();
        text.AppendLine(command.Summary + ".");
        AppendOptions(text, "Options:", command.Options);
        AppendOptions(text, "Global options:", globalOptions);
        return text.ToString();
    }

    private static void AppendOptions(StringBuilder text, string title, IReadOnlyList<OptionSpec> options)
    {
        if (options.Count == 0) return;
        string[] names = options.Select(Describe).ToArray();
        int width = names.Max(name => name.Length) + 3;
        text.AppendLine();
        text.AppendLine(title);
        for (int i = 0; i < options.Count; i++)
            text.AppendLine("  " + names[i].PadRight(width) + options[i].Help);
    }

    private static string Describe(OptionSpec option)
    {
        string name = (option.Alias is null ? string.Empty : "-" + option.Alias + ", ") + "--" + option.Name;
        if (option.Kind == OptionKind.Flag) return name;
        string value = option.Allowed is { } allowed
            ? string.Join('|', allowed)
            : "<" + (option.ValueName ?? "value") + ">";
        return name + " " + value;
    }
}
