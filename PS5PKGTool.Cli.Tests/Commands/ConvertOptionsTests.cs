using PS5PKGTool.Cli.Commands;
using PS5PKGTool.Cli.Parsing;
using PS5PKGTool.Ffpfsc;

namespace PS5PKGTool.Cli.Tests.Commands;

public class ConvertOptionsTests
{
    private static ParsedArgs Args(params string[] args) => ArgParser.Parse(args, ConvertOptions.Specs, "convert");

    [Theory]
    [InlineData("game.exfat", "exfat")]
    [InlineData("game.FFPKG", "ffpkg")]
    [InlineData("dir/game.ffpfsc", "ffpfsc")]
    public void Target_comes_from_the_output_extension(string output, string expected)
    {
        var warnings = new List<string>();

        Assert.Equal(expected, ConvertOptions.NameOf(ConvertOptions.ResolveTarget(null, output, warnings)));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Unknown_extension_without_to_is_a_usage_error()
    {
        UsageException error = Assert.Throws<UsageException>(() => ConvertOptions.ResolveTarget(null, "game.img", []));

        Assert.StartsWith("cannot tell the target format from the output name", error.Message);
    }

    [Fact]
    public void To_overrides_a_different_extension_with_a_warning()
    {
        var warnings = new List<string>();

        Ps5ImageConversionTarget target = ConvertOptions.ResolveTarget("ffpkg", "game.exfat", warnings);

        Assert.Equal(Ps5ImageConversionTarget.Ffpkg, target);
        Assert.Equal(new[] { "--to ffpkg does not match the output extension '.exfat'; writing ffpkg" }, warnings);
    }

    [Fact]
    public void To_with_an_unrelated_extension_has_no_warning()
    {
        var warnings = new List<string>();

        Assert.Equal(Ps5ImageConversionTarget.Exfat, ConvertOptions.ResolveTarget("exfat", "game.img", warnings));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Defaults_match_the_gui_image_tools_defaults()
    {
        ConvertFormatOptions exfat = ConvertOptions.Build(Ps5ImageConversionTarget.Exfat, Args());
        Assert.Null(exfat.Exfat!.ClusterSize);
        Assert.True(exfat.Exfat.GenerateAmprIndex);
        Assert.Null(exfat.Ffpfsc);
        Assert.Null(exfat.Ffpkg);

        ConvertFormatOptions ffpfsc = ConvertOptions.Build(Ps5ImageConversionTarget.Ffpfsc, Args());
        Assert.Null(ffpfsc.Exfat!.ClusterSize);
        Assert.True(ffpfsc.Exfat.GenerateAmprIndex);
        Assert.Equal(7, ffpfsc.Ffpfsc!.Compression.CompressionLevel);
        Assert.Equal(1, ffpfsc.Ffpfsc.Compression.MinimumGainPercent);
        Assert.Null(ffpfsc.Ffpkg);

        ConvertFormatOptions ffpkg = ConvertOptions.Build(Ps5ImageConversionTarget.Ffpkg, Args());
        Assert.Null(ffpkg.Exfat);
        Assert.Null(ffpkg.Ffpfsc);
        Assert.Equal(32768, ffpkg.Ffpkg!.BlockSize);
        Assert.Equal(4096, ffpkg.Ffpkg.FragmentSize);
        Assert.Equal(262144, ffpkg.Ffpkg.BytesPerInode);
        Assert.Equal(0, ffpkg.Ffpkg.MinFreePercent);
    }

    [Fact]
    public void Explicit_values_are_mapped()
    {
        ConvertFormatOptions exfat = ConvertOptions.Build(Ps5ImageConversionTarget.Exfat, Args("--cluster", "64k", "--no-ampr"));
        Assert.Equal(65536, exfat.Exfat!.ClusterSize);
        Assert.False(exfat.Exfat.GenerateAmprIndex);

        ConvertFormatOptions ffpfsc = ConvertOptions.Build(Ps5ImageConversionTarget.Ffpfsc,
            Args("--cluster", "32k", "--level", "9", "--min-gain", "5"));
        Assert.Equal(32768, ffpfsc.Exfat!.ClusterSize);
        Assert.Equal(9, ffpfsc.Ffpfsc!.Compression.CompressionLevel);
        Assert.Equal(5, ffpfsc.Ffpfsc.Compression.MinimumGainPercent);

        ConvertFormatOptions ffpkg = ConvertOptions.Build(Ps5ImageConversionTarget.Ffpkg,
            Args("--block", "64k", "--fragment", "64k", "--inode-density", "1m", "--min-free", "10"));
        Assert.Equal(65536, ffpkg.Ffpkg!.BlockSize);
        Assert.Equal(65536, ffpkg.Ffpkg.FragmentSize);
        Assert.Equal(1048576, ffpkg.Ffpkg.BytesPerInode);
        Assert.Equal(10, ffpkg.Ffpkg.MinFreePercent);
        Assert.Empty(ffpkg.Warnings);
    }

    [Fact]
    public void Fragment_larger_than_block_is_capped_with_a_warning()
    {
        ConvertFormatOptions options = ConvertOptions.Build(Ps5ImageConversionTarget.Ffpkg, Args("--fragment", "64k"));

        Assert.Equal(32768, options.Ffpkg!.FragmentSize);
        Assert.Equal(new[] { "--fragment 64k is larger than --block 32k; using a 32k fragment" }, options.Warnings);
    }

    [Theory]
    [InlineData("exfat", "--level", "5", "--level only applies to --to ffpfsc")]
    [InlineData("exfat", "--min-gain", "5", "--min-gain only applies to --to ffpfsc")]
    [InlineData("exfat", "--block", "64k", "--block only applies to --to ffpkg")]
    [InlineData("ffpfsc", "--min-free", "5", "--min-free only applies to --to ffpkg")]
    [InlineData("ffpkg", "--cluster", "32k", "--cluster only applies to --to exfat or ffpfsc")]
    public void Options_for_another_format_are_rejected(string target, string option, string value, string expected)
    {
        Ps5ImageConversionTarget resolved = ConvertOptions.ResolveTarget(target, "out.img", []);

        UsageException error = Assert.Throws<UsageException>(() => ConvertOptions.Build(resolved, Args(option, value)));

        Assert.Equal(expected, error.Message);
        Assert.Equal("convert", error.Command);
    }

    [Fact]
    public void No_ampr_is_rejected_for_ffpkg()
    {
        UsageException error = Assert.Throws<UsageException>(() =>
            ConvertOptions.Build(Ps5ImageConversionTarget.Ffpkg, Args("--no-ampr")));

        Assert.Equal("--no-ampr only applies to --to exfat or ffpfsc", error.Message);
    }
}
