using PS5PKGTool.Cli.Output;
using PS5PKGTool.Cli.Parsing;
using PS5PKGTool.Core.Models;
using PS5PKGTool.Core.Services;

namespace PS5PKGTool.Cli.Commands;

internal sealed class ScanCommand : ICommand
{
    private const int TitleWidth = 40;

    public string Name => "scan";
    public string Summary => "List every dump, package and image found in folders";
    public string Usage => "ps5pkg scan <folder>... [--no-recurse] [--json]";

    public IReadOnlyList<OptionSpec> Options { get; } =
    [
        new("no-recurse", OptionKind.Flag, "Only look at the top level of each folder"),
        new("json", OptionKind.Flag, "Print the records as a JSON array"),
    ];

    public async Task<int> RunAsync(ParsedArgs args, CommandContext context, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count == 0) throw new UsageException("missing <folder>", Name);
        foreach (string folder in args.Positionals)
        {
            if (!Directory.Exists(folder) && !File.Exists(folder))
                throw new DirectoryNotFoundException($"no such file or directory: {folder}");
        }
        cancellationToken.ThrowIfCancellationRequested();

        Ps5ScanResult result = await new Ps5LibraryScanner()
            .ScanAsync(args.Positionals, recursive: !args.Has("no-recurse"), progress: context.Progress,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        context.Progress.Finish();
        DumpSize.Fill(result.Games, cancellationToken);

        foreach (string error in result.Errors) context.Error.WriteLine("warning: " + error);
        Ps5GameInfo[] games = result.Games
            .OrderBy(game => game.TitleId, StringComparer.Ordinal)
            .ThenBy(game => game.RootPath, StringComparer.Ordinal)
            .ToArray();

        if (args.Has("json"))
            JsonOutput.Write(context.Out, games);
        else if (games.Length == 0)
            context.Error.WriteLine("no PS5 titles found");
        else
            TableWriter.Write(context.Out, ["Title ID", "Version", "Category", "Format", "Size", "Title", "Path"],
                games.Select(Row).ToArray());
        return result.Errors.Count == 0 ? 0 : 1;
    }

    private static string[] Row(Ps5GameInfo game) =>
    [
        Cell(game.TitleId),
        Cell(game.DisplayVersion),
        Cell(game.ApplicationCategory),
        game.SourceDescription,
        SizeFormat.Bytes(game.SourceSize),
        TableWriter.Truncate(Cell(game.Title), TitleWidth),
        game.RootPath,
    ];

    private static string Cell(string value) => string.IsNullOrWhiteSpace(value) ? "-" : TableWriter.Clean(value);
}
