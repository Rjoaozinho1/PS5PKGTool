using PS5PKGTool.Cli.Output;

namespace PS5PKGTool.Cli.Tests.Output;

public class TableWriterTests
{
    [Fact]
    public void Pads_columns_to_the_widest_cell_and_leaves_the_last_column_unpadded()
    {
        var output = new StringWriter { NewLine = "\n" };

        TableWriter.Write(output, ["ID", "Title", "Path"], [["PPSA1", "A", "/a"], ["P2", "Longer", "/b/c"]]);

        Assert.Equal(
            "ID     Title   Path\n" +
            "PPSA1  A       /a\n" +
            "P2     Longer  /b/c\n",
            output.ToString());
    }

    [Fact]
    public void Clean_replaces_control_characters_with_spaces() =>
        Assert.Equal("Line one Line two", TableWriter.Clean("Line one\nLine\ttwo"));

    [Fact]
    public void Truncate_shortens_long_values_with_an_ellipsis()
    {
        Assert.Equal("abc", TableWriter.Truncate("abc", 5));
        Assert.Equal("abcd…", TableWriter.Truncate("abcdefgh", 5));
    }
}
