using PS5PKGTool.Cli.Output;

namespace PS5PKGTool.Cli.Tests.Output;

public class SizeFormatTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(3006477107L, "2.8 GB")]
    public void Formats_bytes_with_1024_based_units(long bytes, string expected) =>
        Assert.Equal(expected, SizeFormat.Bytes(bytes));
}
