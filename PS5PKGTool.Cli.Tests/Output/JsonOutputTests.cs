using System.Text.Json;
using PS5PKGTool.Cli.Output;
using PS5PKGTool.Core.Models;

namespace PS5PKGTool.Cli.Tests.Output;

public class JsonOutputTests
{
    [Fact]
    public void Uses_camel_case_names_string_enums_and_keeps_non_ascii_text()
    {
        var game = new Ps5GameInfo { TitleId = "PPSA99999", Title = "ゲーム", SourceKind = Ps5SourceKind.Ffpkg };
        var output = new StringWriter { NewLine = "\n" };

        JsonOutput.Write(output, game);

        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.Equal("PPSA99999", document.RootElement.GetProperty("titleId").GetString());
        Assert.Equal("Ffpkg", document.RootElement.GetProperty("sourceKind").GetString());
        Assert.Contains("ゲーム", output.ToString());
    }
}
