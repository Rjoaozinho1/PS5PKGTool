using System.Globalization;

namespace PS5PKGTool.Cli.Output;

internal static class SizeFormat
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB"];

    /// <summary>1024-based size with one decimal, for example "512 B", "12.3 KB", "4.0 GB".</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        double value = bytes / 1024.0;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return value.ToString("0.0", CultureInfo.InvariantCulture) + " " + Units[unit];
    }
}
