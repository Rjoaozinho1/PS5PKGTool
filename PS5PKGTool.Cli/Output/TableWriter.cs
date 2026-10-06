using System.Text;

namespace PS5PKGTool.Cli.Output;

internal static class TableWriter
{
    /// <summary>Writes left-aligned columns separated by two spaces. The last column is never padded.</summary>
    public static void Write(TextWriter output, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        int[] widths = new int[headers.Count];
        for (int column = 0; column < headers.Count; column++)
        {
            widths[column] = headers[column].Length;
            foreach (string[] row in rows) widths[column] = Math.Max(widths[column], row[column].Length);
        }

        WriteRow(output, headers, widths);
        foreach (string[] row in rows) WriteRow(output, row, widths);
    }

    /// <summary>Replaces control characters (newlines, tabs) so a value stays on one line.</summary>
    public static string Clean(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char character in value) builder.Append(char.IsControl(character) ? ' ' : character);
        return builder.ToString();
    }

    public static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private static void WriteRow(TextWriter output, IReadOnlyList<string> cells, int[] widths)
    {
        var line = new StringBuilder();
        for (int column = 0; column < cells.Count; column++)
        {
            if (column > 0) line.Append("  ");
            bool last = column == cells.Count - 1;
            line.Append(last ? cells[column] : cells[column].PadRight(widths[column]));
        }
        output.WriteLine(line.ToString());
    }
}
