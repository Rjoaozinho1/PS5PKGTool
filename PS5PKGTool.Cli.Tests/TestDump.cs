namespace PS5PKGTool.Cli.Tests;

/// <summary>Builds a small synthetic PS5 dump. No real game data is involved.</summary>
internal static class TestDump
{
    public const string TitleId = "PPSA99999";
    public const string ContentId = "UP9999-PPSA99999_00-CLITESTFIXTURE00";
    public const string Title = "CLI Fixture";

    /// <summary>Creates <paramref name="parent"/>/<paramref name="folderName"/> and returns its full path.</summary>
    public static string Create(string parent, string folderName = "PPSA99999-app", string? paramJson = null)
    {
        string root = Path.Combine(parent, folderName);
        Directory.CreateDirectory(Path.Combine(root, "sce_sys"));
        File.WriteAllText(Path.Combine(root, "sce_sys", "param.json"), paramJson ?? ParamJson());
        File.WriteAllBytes(Path.Combine(root, "eboot.bin"), RandomBytes(64 * 1024, seed: 1));
        Directory.CreateDirectory(Path.Combine(root, "data", "nested"));
        File.WriteAllBytes(Path.Combine(root, "data", "level.dat"), RandomBytes(300 * 1024, seed: 2)); // several blocks, incompressible
        File.WriteAllBytes(Path.Combine(root, "data", "zeros.bin"), new byte[200 * 1024]);               // compressible
        File.WriteAllText(Path.Combine(root, "data", "nested", "readme.txt"), "fixture text\n");
        return root;
    }

    /// <summary>A minimal param.json. <paramref name="titleNameJson"/> is inserted as-is, so escape it for JSON.</summary>
    public static string ParamJson(string titleNameJson = Title) => $$"""
        {
          "titleId": "{{TitleId}}",
          "contentId": "{{ContentId}}",
          "contentVersion": "01.000.000",
          "masterVersion": "01.00",
          "applicationCategoryType": 0,
          "applicationDrmType": "standard",
          "requiredSystemSoftwareVersion": "0x0400000000000000",
          "sdkVersion": "0x0400000000000000",
          "localizedParameters": {
            "defaultLanguage": "en-US",
            "en-US": { "titleName": "{{titleNameJson}}" }
          }
        }
        """;

    private static byte[] RandomBytes(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }
}
