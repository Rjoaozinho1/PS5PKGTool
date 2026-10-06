using System.Reflection;
using PS5PKGTool.Cli.Commands;
using PS5PKGTool.Cli.Output;
using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli;

internal static class CliApp
{
    public static readonly IReadOnlyList<OptionSpec> GlobalOptions =
    [
        new("help", OptionKind.Flag, "Show this help") { Alias = "h" },
        new("quiet", OptionKind.Flag, "Do not show progress"),
        new("debug", OptionKind.Flag, "Show stack traces on errors"),
    ];

    public static IReadOnlyList<ICommand> DefaultCommands { get; } = [new InfoCommand()];

    public static string Version
    {
        get
        {
            string version = typeof(CliApp).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            int plus = version.IndexOf('+'); // drop the source-revision suffix the SDK appends
            return plus < 0 ? version : version[..plus];
        }
    }

    public static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, bool isErrorTerminal,
        CancellationToken cancellationToken) =>
        RunAsync(args, stdout, stderr, isErrorTerminal, cancellationToken, DefaultCommands);

    internal static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr,
        bool isErrorTerminal, CancellationToken cancellationToken, IReadOnlyList<ICommand> commands)
    {
        // The library reports progress from thread-pool threads; keep stderr writes from interleaving.
        TextWriter error = TextWriter.Synchronized(stderr);
        bool debug = args.Contains("--debug");
        ProgressReporter? progress = null;
        try
        {
            int index = 0;
            bool quiet = false;
            for (; index < args.Length && args[index].StartsWith('-'); index++)
            {
                switch (args[index])
                {
                    case "-h" or "--help":
                        stdout.Write(HelpText.General(commands));
                        return 0;
                    case "--version":
                        stdout.WriteLine("ps5pkg " + Version);
                        return 0;
                    case "--quiet":
                        quiet = true;
                        break;
                    case "--debug":
                        break;
                    default:
                        throw new UsageException($"unknown option '{args[index]}'");
                }
            }
            if (index == args.Length)
            {
                error.Write(HelpText.General(commands));
                return 2;
            }

            string name = args[index];
            ICommand command = commands.FirstOrDefault(candidate => candidate.Name == name)
                ?? throw new UsageException($"unknown command '{name}'");
            ParsedArgs parsed = ArgParser.Parse(args[(index + 1)..], [.. command.Options, .. GlobalOptions], command.Name);
            if (parsed.Has("help"))
            {
                stdout.Write(HelpText.For(command, GlobalOptions));
                return 0;
            }

            quiet |= parsed.Has("quiet");
            ProgressMode mode = quiet ? ProgressMode.Off : isErrorTerminal ? ProgressMode.Terminal : ProgressMode.Lines;
            progress = new ProgressReporter(error, mode);
            return await command.RunAsync(parsed, new CommandContext(stdout, error, progress), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UsageException ex)
        {
            progress?.Finish();
            error.WriteLine("error: " + ex.Message);
            error.WriteLine(ex.Command is null ? "run 'ps5pkg --help'" : $"run 'ps5pkg {ex.Command} --help'");
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            progress?.Finish();
            error.WriteLine("cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            progress?.Finish();
            error.WriteLine("error: " + ex.Message);
            if (debug) error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            progress?.Finish();
        }
    }
}
