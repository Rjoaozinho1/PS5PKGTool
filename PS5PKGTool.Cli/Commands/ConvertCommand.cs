using PS5PKGTool.Cli.Output;
using PS5PKGTool.Cli.Parsing;
using PS5PKGTool.Core.Services;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Commands;

/// <summary>
/// Creates or converts images through the same two library calls the GUI uses
/// (MainForm.ConvertImageAsync): SonyPackageImageConversion for .pkg sources and
/// Ps5ImageConversionService for everything else. Every check runs before any work starts.
/// </summary>
internal sealed class ConvertCommand : ICommand
{
    private enum SourceKind
    {
        DumpFolder,
        Package,
        ExfatImage,
        Ufs2Image,
        PfsImage
    }

    public string Name => ConvertOptions.Command;
    public string Summary => "Create an image from a dump, or convert an image or debug .pkg to another image format";
    public string Usage =>
        "ps5pkg convert <source> -o <output> [--to exfat|ffpkg|ffpfsc] [--force] [--temp <dir>] [format options]";
    public IReadOnlyList<OptionSpec> Options => ConvertOptions.Specs;

    public async Task<int> RunAsync(ParsedArgs args, CommandContext context, CancellationToken cancellationToken)
    {
        string source = args.Positionals.Count switch
        {
            0 => throw new UsageException("missing <source>", Name),
            1 => args.Positionals[0],
            _ => throw new UsageException("expected one <source>", Name),
        };
        string output = args.Value("output") ?? throw new UsageException("missing -o/--output <path>", Name);

        var warnings = new List<string>();
        Ps5ImageConversionTarget target = ConvertOptions.ResolveTarget(args.Value("to"), output, warnings);
        ConvertFormatOptions options = ConvertOptions.Build(target, args);
        warnings.AddRange(options.Warnings);

        string sourceFull = Path.GetFullPath(source);
        string outputFull = Path.GetFullPath(output);
        SourceKind kind = Classify(sourceFull, source);
        bool isImage = kind is SourceKind.ExfatImage or SourceKind.Ufs2Image or SourceKind.PfsImage;
        // The library stores an exFAT/UFS2 image as-is inside FFPFSC (ImageConversionService direct wrap).
        bool wrapsImage = (kind is SourceKind.ExfatImage or SourceKind.Ufs2Image) && target == Ps5ImageConversionTarget.Ffpfsc;

        if (wrapsImage && (args.Has("cluster") || args.Has("no-ampr")))
        {
            throw new UsageException(
                "--cluster and --no-ampr have no effect when wrapping an exFAT or FFPKG image into FFPFSC " +
                "(the image is stored as-is)", Name);
        }
        if (kind == SourceKind.DumpFolder && IsInside(outputFull, sourceFull))
            throw new UsageException("the output must not be inside the source dump folder", Name);
        if (isImage && !wrapsImage && !Ps5ImageConversionService.IsSupported(FormatOf(kind), target))
        {
            throw new InvalidDataException(
                $"converting this {Describe(kind)} image to {ConvertOptions.NameOf(target)} is not supported");
        }

        bool overwrite = args.Has("force");
        CheckOutput(outputFull, output, overwrite);
        string? temp = ResolveTemp(args.Value("temp"));

        foreach (string warning in warnings) context.Error.WriteLine("warning: " + warning);
        if (kind == SourceKind.Package || (isImage && !wrapsImage))
            context.Error.WriteLine("note: extracting to " + (temp ?? Path.GetTempPath()));

        cancellationToken.ThrowIfCancellationRequested();
        Ps5ImageConversionResult result = kind == SourceKind.Package
            ? await SonyPackageImageConversion.ConvertAsync(sourceFull, outputFull, target, overwrite, context.Progress,
                cancellationToken, options.Exfat, options.Ffpfsc, options.Ffpkg, temp).ConfigureAwait(false)
            : await Ps5ImageConversionService.ConvertAsync(sourceFull, outputFull, target, overwrite, context.Progress,
                cancellationToken, options.Exfat, options.Ffpfsc, options.Ffpkg, temp).ConfigureAwait(false);
        context.Progress.Finish();

        string files = result.FileCount == 1 ? "1 file" : $"{result.FileCount} files";
        context.Out.WriteLine(
            $"{result.OutputPath}  {ConvertOptions.NameOf(result.Target)}  {SizeFormat.Bytes(result.OutputBytes)}  ({files})");
        return 0;
    }

    private static SourceKind Classify(string sourceFull, string source)
    {
        if (Directory.Exists(sourceFull))
        {
            if (!File.Exists(Path.Combine(sourceFull, "sce_sys", "param.json")))
                throw new InvalidDataException($"{source} is not a PS5 dump folder (no sce_sys/param.json)");
            return SourceKind.DumpFolder;
        }
        if (!File.Exists(sourceFull))
            throw new FileNotFoundException($"no such file or directory: {source}");
        if (Path.GetExtension(sourceFull).Equals(".pkg", StringComparison.OrdinalIgnoreCase))
            return SourceKind.Package;
        return Ps5ImageConversionService.DetectSource(sourceFull) switch
        {
            Ps5ImageFormat.Exfat => SourceKind.ExfatImage,
            Ps5ImageFormat.Ufs2 => SourceKind.Ufs2Image,
            Ps5ImageFormat.Pfs => SourceKind.PfsImage,
            _ => throw new InvalidDataException(
                $"{source} is not a PS5 dump folder, debug .pkg, or exFAT/FFPKG/FFPFSC image"),
        };
    }

    private static Ps5ImageFormat FormatOf(SourceKind kind) => kind switch
    {
        SourceKind.ExfatImage => Ps5ImageFormat.Exfat,
        SourceKind.Ufs2Image => Ps5ImageFormat.Ufs2,
        SourceKind.PfsImage => Ps5ImageFormat.Pfs,
        _ => Ps5ImageFormat.Unknown,
    };

    private static string Describe(SourceKind kind) => kind switch
    {
        SourceKind.ExfatImage => "exFAT",
        SourceKind.Ufs2Image => "FFPKG",
        _ => "FFPFSC",
    };

    private static void CheckOutput(string outputFull, string output, bool overwrite)
    {
        if (Directory.Exists(outputFull))
            throw new IOException($"the output path is a folder: {output}");
        string? folder = Path.GetDirectoryName(outputFull);
        if (folder is null || !Directory.Exists(folder))
            throw new DirectoryNotFoundException($"the output folder does not exist: {folder ?? output}");
        // The library only notices an existing output after the full build for dump and .pkg sources.
        if (File.Exists(outputFull) && !overwrite)
            throw new IOException($"the output already exists: {output} (use --force to overwrite)");
    }

    private static string? ResolveTemp(string? temp)
    {
        if (temp is null) return null;
        string full = Path.GetFullPath(temp);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"the temp folder does not exist: {temp}");
        return full;
    }

    private static bool IsInside(string path, string folder)
    {
        string prefix = Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix,
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }
}
