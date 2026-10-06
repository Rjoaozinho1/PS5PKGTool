using PS5PKGTool.Cli.Parsing;

namespace PS5PKGTool.Cli.Tests.Parsing;

public class ArgParserTests
{
    private static readonly OptionSpec[] Specs =
    [
        new("output", OptionKind.Value, "Output path") { Alias = "o", ValueName = "path" },
        new("force", OptionKind.Flag, "Overwrite"),
        new("level", OptionKind.Value, "Level") { Range = (1, 9) },
        new("to", OptionKind.Value, "Target") { Allowed = ["exfat", "ffpkg", "ffpfsc"] },
    ];

    private static ParsedArgs Parse(params string[] args) => ArgParser.Parse(args, Specs, "convert");

    [Fact]
    public void Positionals_and_options_are_separated()
    {
        ParsedArgs parsed = Parse("dump", "-o", "out.exfat", "--force");

        Assert.Equal(new[] { "dump" }, parsed.Positionals);
        Assert.Equal("out.exfat", parsed.Value("output"));
        Assert.True(parsed.Has("force"));
        Assert.False(parsed.Has("level"));
    }

    [Fact]
    public void Long_option_accepts_an_inline_value() =>
        Assert.Equal("x.ffpkg", Parse("--output=x.ffpkg").Value("output"));

    [Fact]
    public void Inline_value_may_start_with_a_dash() =>
        Assert.Equal("-odd", Parse("--output=-odd").Value("output"));

    [Fact]
    public void Double_dash_ends_options()
    {
        ParsedArgs parsed = Parse("--", "--force", "-o");

        Assert.Equal(new[] { "--force", "-o" }, parsed.Positionals);
        Assert.False(parsed.Has("force"));
    }

    [Fact]
    public void Single_dash_is_positional() => Assert.Equal(new[] { "-" }, Parse("-").Positionals);

    [Fact]
    public void Allowed_values_match_case_insensitively_and_are_normalised() =>
        Assert.Equal("ffpkg", Parse("--to", "FFPKG").Value("to"));

    [Fact]
    public void Range_values_are_parsed() => Assert.Equal(9, Parse("--level", "09").Int("level"));

    [Fact]
    public void Repeated_flags_are_allowed() => Assert.True(Parse("--force", "--force").Has("force"));

    [Theory]
    [InlineData(new[] { "--bogus" }, "unknown option '--bogus'")]
    [InlineData(new[] { "--bogus=1" }, "unknown option '--bogus'")]
    [InlineData(new[] { "-x" }, "unknown option '-x'")]
    [InlineData(new[] { "--force=yes" }, "option '--force' does not take a value")]
    [InlineData(new[] { "-o" }, "option '--output' needs a value")]
    [InlineData(new[] { "-o", "--force" }, "option '--output' needs a value")]
    [InlineData(new[] { "--output=" }, "option '--output' needs a value")]
    [InlineData(new[] { "-o", "a", "-o", "b" }, "option '--output' was given more than once")]
    [InlineData(new[] { "--level", "10" }, "'--level' must be a whole number from 1 to 9")]
    [InlineData(new[] { "--level", "abc" }, "'--level' must be a whole number from 1 to 9")]
    [InlineData(new[] { "--to", "iso" }, "invalid value 'iso' for '--to' (expected exfat, ffpkg, ffpfsc)")]
    public void Bad_usage_is_reported(string[] args, string expected)
    {
        UsageException error = Assert.Throws<UsageException>(() => Parse(args));

        Assert.StartsWith(expected, error.Message);
        Assert.Equal("convert", error.Command);
    }
}
