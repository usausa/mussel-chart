namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

internal readonly record struct ChartTipRow(string? Color, string? Name, string? Value);

internal readonly record struct ChartTipMark(double X, double Y, string Color);

internal readonly record struct ChartTipLine(double X1, double Y1, double X2, double Y2);

// The text of a tooltip travels in a data attribute, so that hovering needs no call to the server
internal static class ChartTip
{
    public static string Create(string? title, IReadOnlyList<ChartTipRow> rows, IReadOnlyList<ChartTipMark>? marks = null, ChartTipLine? line = null)
    {
        var builder = ChartStringBuilder.Rent();
        builder.Append("{\"t\":");
        AppendString(builder, title);
        builder.Append(",\"r\":[");
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append('[');
            AppendString(builder, rows[i].Color);
            builder.Append(',');
            AppendString(builder, rows[i].Name);
            builder.Append(',');
            AppendString(builder, rows[i].Value);
            builder.Append(']');
        }

        builder.Append(']');
        if (marks is { Count: > 0 })
        {
            builder.Append(",\"m\":[");
            for (var i = 0; i < marks.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append('[');
                AppendNumber(builder, marks[i].X);
                builder.Append(',');
                AppendNumber(builder, marks[i].Y);
                builder.Append(',');
                AppendString(builder, marks[i].Color);
                builder.Append(']');
            }

            builder.Append(']');
        }

        if (line is { } value)
        {
            builder.Append(",\"l\":[");
            AppendNumber(builder, value.X1);
            builder.Append(',');
            AppendNumber(builder, value.Y1);
            builder.Append(',');
            AppendNumber(builder, value.X2);
            builder.Append(',');
            AppendNumber(builder, value.Y2);
            builder.Append(']');
        }

        builder.Append('}');
        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    public static string CreateStrings(IReadOnlyList<string> values)
    {
        var builder = ChartStringBuilder.Rent();
        builder.Append('[');
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendString(builder, values[i]);
        }

        builder.Append(']');
        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    private static void AppendNumber(StringBuilder builder, double value) =>
        builder.Append(CultureInfo.InvariantCulture, $"{value:0.#}");

    private static void AppendString(StringBuilder builder, string? value)
    {
        builder.Append('"');
        foreach (var c in value ?? string.Empty)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case < ' ':
                    builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        builder.Append('"');
    }
}
