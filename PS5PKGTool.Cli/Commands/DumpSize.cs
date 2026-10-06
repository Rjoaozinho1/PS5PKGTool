using PS5PKGTool.Core.Models;

namespace PS5PKGTool.Cli.Commands;

/// <summary>
/// The library scanner leaves <see cref="Ps5GameInfo.SourceSize"/> at 0 for loose dumps; the GUI measures the
/// folder afterwards (MainForm.MeasureDirectory). This does the same so info and scan show the real size.
/// </summary>
internal static class DumpSize
{
    public static void Fill(IEnumerable<Ps5GameInfo> games, CancellationToken cancellationToken)
    {
        foreach (Ps5GameInfo game in games)
        {
            if (game.SourceKind != Ps5SourceKind.LooseDump || game.SourceSize > 0 || !Directory.Exists(game.RootPath))
                continue;
            game.SourceSize = Measure(game.RootPath, cancellationToken);
        }
    }

    private static long Measure(string root, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        long total = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { total = checked(total + new FileInfo(path).Length); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return total;
    }
}
