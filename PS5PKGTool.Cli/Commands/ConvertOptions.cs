using PS5PKGTool.Cli.Parsing;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Commands;

/// <summary>Library option objects for one conversion. Unused ones are null, exactly as the GUI passes them.</summary>
internal sealed record ConvertFormatOptions(
    ExfatBuildOptions? Exfat, FfpfscBuildOptions? Ffpfsc, FfpkgBuildOptions? Ffpkg, IReadOnlyList<string> Warnings);

internal static class ConvertOptions
{
    public const string Command = "convert";

    // GUI Image Tools defaults (MainForm.Designer.cs: nudImageLevel = 7, nudImageGain = 1), which differ
    // from the PfscCompressionOptions class defaults (9 and 5).
    private const int DefaultLevel = 7;
    private const int DefaultMinimumGain = 1;

    public static IReadOnlyList<OptionSpec> Specs { get; } =
    [
        new("output", OptionKind.Value, "Output image path (required)") { Alias = "o", ValueName = "path" },
        new("to", OptionKind.Value, "Target format (default: from the output extension)") { Allowed = ["exfat", "ffpkg", "ffpfsc"] },
        new("force", OptionKind.Flag, "Overwrite the output if it exists"),
        new("temp", OptionKind.Value, "Folder for extracting an image or .pkg source (default: $TMPDIR or /tmp)") { ValueName = "dir" },
        new("cluster", OptionKind.Value, "exFAT/FFPFSC: exFAT cluster size (default auto)") { Allowed = ["auto", "32k", "64k"] },
        new("no-ampr", OptionKind.Flag, "exFAT/FFPFSC: do not generate the AMPR index"),
        new("level", OptionKind.Value, "FFPFSC: zlib level (default 7)") { ValueName = "1-9", Range = (1, 9) },
        new("min-gain", OptionKind.Value, "FFPFSC: minimum % saved to keep a block compressed (default 1)") { ValueName = "0-100", Range = (0, 100) },
        new("block", OptionKind.Value, "FFPKG: block size (default 32k)") { Allowed = ["32k", "64k"] },
        new("fragment", OptionKind.Value, "FFPKG: fragment size (default 4k)") { Allowed = ["4k", "64k"] },
        new("inode-density", OptionKind.Value, "FFPKG: bytes per inode (default 256k)") { Allowed = ["256k", "512k", "1m"] },
        new("min-free", OptionKind.Value, "FFPKG: reserved free space % (default 0)") { ValueName = "0-50", Range = (0, 50) },
    ];

    public static string NameOf(Ps5ImageConversionTarget target) => target switch
    {
        Ps5ImageConversionTarget.Exfat => "exfat",
        Ps5ImageConversionTarget.Ffpkg => "ffpkg",
        _ => "ffpfsc",
    };

    /// <summary>Uses --to when given (warning if the extension disagrees), otherwise the output extension.</summary>
    public static Ps5ImageConversionTarget ResolveTarget(string? to, string output, List<string> warnings)
    {
        string extension = Path.GetExtension(output);
        Ps5ImageConversionTarget? fromExtension = extension.ToLowerInvariant() switch
        {
            ".exfat" => Ps5ImageConversionTarget.Exfat,
            ".ffpkg" => Ps5ImageConversionTarget.Ffpkg,
            ".ffpfsc" => Ps5ImageConversionTarget.Ffpfsc,
            _ => null,
        };
        if (to is null)
        {
            return fromExtension ?? throw new UsageException(
                "cannot tell the target format from the output name; pass --to exfat|ffpkg|ffpfsc", Command);
        }

        Ps5ImageConversionTarget chosen = to switch
        {
            "exfat" => Ps5ImageConversionTarget.Exfat,
            "ffpkg" => Ps5ImageConversionTarget.Ffpkg,
            _ => Ps5ImageConversionTarget.Ffpfsc,
        };
        if (fromExtension is { } inferred && inferred != chosen)
            warnings.Add($"--to {to} does not match the output extension '{extension}'; writing {to}");
        return chosen;
    }

    public static ConvertFormatOptions Build(Ps5ImageConversionTarget target, ParsedArgs args)
    {
        RequireTarget(target, args, "cluster", Ps5ImageConversionTarget.Exfat, Ps5ImageConversionTarget.Ffpfsc);
        RequireTarget(target, args, "no-ampr", Ps5ImageConversionTarget.Exfat, Ps5ImageConversionTarget.Ffpfsc);
        RequireTarget(target, args, "level", Ps5ImageConversionTarget.Ffpfsc);
        RequireTarget(target, args, "min-gain", Ps5ImageConversionTarget.Ffpfsc);
        RequireTarget(target, args, "block", Ps5ImageConversionTarget.Ffpkg);
        RequireTarget(target, args, "fragment", Ps5ImageConversionTarget.Ffpkg);
        RequireTarget(target, args, "inode-density", Ps5ImageConversionTarget.Ffpkg);
        RequireTarget(target, args, "min-free", Ps5ImageConversionTarget.Ffpkg);

        var warnings = new List<string>();

        // FFPFSC wraps an exFAT image, so it takes exFAT options too (as in MainForm.RunImageConvert).
        ExfatBuildOptions? exfat = target is Ps5ImageConversionTarget.Exfat or Ps5ImageConversionTarget.Ffpfsc
            ? new ExfatBuildOptions
            {
                ClusterSize = args.Value("cluster") switch { "32k" => 32768, "64k" => 65536, _ => null },
                GenerateAmprIndex = !args.Has("no-ampr"),
            }
            : null;

        FfpfscBuildOptions? ffpfsc = target == Ps5ImageConversionTarget.Ffpfsc
            ? new FfpfscBuildOptions
            {
                Compression = new PfscCompressionOptions
                {
                    CompressionLevel = args.Int("level") ?? DefaultLevel,
                    MinimumGainPercent = args.Int("min-gain") ?? DefaultMinimumGain,
                },
            }
            : null;

        FfpkgBuildOptions? ffpkg = null;
        if (target == Ps5ImageConversionTarget.Ffpkg)
        {
            int block = args.Value("block") == "64k" ? 65536 : 32768;
            int fragment = args.Value("fragment") == "64k" ? 65536 : 4096;
            if (fragment > block)
            {
                warnings.Add("--fragment 64k is larger than --block 32k; using a 32k fragment");
                fragment = block;
            }
            ffpkg = new FfpkgBuildOptions
            {
                BlockSize = block,
                FragmentSize = fragment,
                BytesPerInode = args.Value("inode-density") switch { "512k" => 524288, "1m" => 1048576, _ => 262144 },
                MinFreePercent = args.Int("min-free") ?? 0,
            };
        }

        return new ConvertFormatOptions(exfat, ffpfsc, ffpkg, warnings);
    }

    private static void RequireTarget(Ps5ImageConversionTarget target, ParsedArgs args, string option,
        params Ps5ImageConversionTarget[] allowed)
    {
        if (args.Has(option) && !allowed.Contains(target))
        {
            throw new UsageException(
                $"--{option} only applies to --to {string.Join(" or ", allowed.Select(NameOf))}", Command);
        }
    }
}
