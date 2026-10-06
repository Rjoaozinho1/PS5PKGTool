using PS5PKGTool.Cli.Output;
using PS5PKGTool.Cli.Parsing;
using PS5PKGTool.Core.Models;
using PS5PKGTool.Core.Services;

namespace PS5PKGTool.Cli.Commands;

internal sealed class InfoCommand : ICommand
{
    private const int KeyWidth = 19; // "Required firmware: "

    public string Name => "info";
    public string Summary => "Show the metadata of one dump, package or image";
    public string Usage => "ps5pkg info <path> [--json]";

    public IReadOnlyList<OptionSpec> Options { get; } =
    [
        new("json", OptionKind.Flag, "Print the full metadata record as JSON"),
    ];

    public async Task<int> RunAsync(ParsedArgs args, CommandContext context, CancellationToken cancellationToken)
    {
        string path = args.Positionals.Count switch
        {
            0 => throw new UsageException("missing <path>", Name),
            1 => args.Positionals[0],
            _ => throw new UsageException("expected one <path>; use 'ps5pkg scan' for several", Name),
        };
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"no such file or directory: {path}");
        cancellationToken.ThrowIfCancellationRequested();

        Ps5ScanResult result = await new Ps5LibraryScanner()
            .ScanAsync([path], recursive: false, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (result.Games.Count == 0)
        {
            throw new InvalidDataException(result.Errors.Count > 0
                ? string.Join("; ", result.Errors)
                : $"no PS5 title found at {path}");
        }
        if (result.Games.Count > 1)
            throw new InvalidDataException($"found {result.Games.Count} titles at {path}; use 'ps5pkg scan'");

        Ps5GameInfo game = result.Games[0];
        DumpSize.Fill([game], cancellationToken);
        foreach (string warning in game.DataWarnings) context.Error.WriteLine("warning: " + warning);
        if (args.Has("json")) JsonOutput.Write(context.Out, game);
        else WriteText(context.Out, game);
        return 0;
    }

    private static void WriteText(TextWriter output, Ps5GameInfo game)
    {
        Line(output, "Title", game.Title);
        Line(output, "Title ID", game.TitleId);
        Line(output, "Content ID", game.ContentId);
        Line(output, "Version", game.DisplayVersion);
        Line(output, "Category", game.ApplicationCategory);
        Line(output, "Required firmware", game.RequiredSystemSoftware);
        Line(output, "SDK", game.SdkVersion);
        Line(output, "DRM type", game.DrmType);
        Line(output, "Format", game.SourceDescription);
        Line(output, "Size", SizeFormat.Bytes(game.SourceSize));
        Line(output, "Path", game.RootPath);
    }

    private static void Line(TextWriter output, string key, string value) =>
        output.WriteLine((key + ":").PadRight(KeyWidth) +
            (string.IsNullOrWhiteSpace(value) ? "-" : TableWriter.Clean(value)));
}
