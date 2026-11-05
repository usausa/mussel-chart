namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

// Angles are degrees clockwise from 12 o'clock
internal static class ChartGeometry
{
    private const double FullCircle = 360;

    private const double Tolerance = 1e-6;

    public static double Normalize(double angle)
    {
        var value = angle % FullCircle;
        return value < 0 ? value + FullCircle : value;
    }

    public static double GetClockAngle(double dx, double dy) => Math.Atan2(dx, -dy) * 180 / Math.PI;

    public static (double X, double Y) GetDirection(double clockAngle)
    {
        var radians = clockAngle * Math.PI / 180;
        return (Math.Sin(radians), -Math.Cos(radians));
    }

    public static (double Left, double Top, double Right, double Bottom) GetUnitBounds(double startAngle, double sweepAngle, double innerRatio)
    {
        if (sweepAngle >= FullCircle)
        {
            return (-1, -1, 1, 1);
        }

        var left = Double.PositiveInfinity;
        var top = Double.PositiveInfinity;
        var right = Double.NegativeInfinity;
        var bottom = Double.NegativeInfinity;

        void Include(double x, double y)
        {
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        var (startX, startY) = GetDirection(startAngle);
        var (endX, endY) = GetDirection(startAngle + sweepAngle);
        Include(startX, startY);
        Include(endX, endY);
        Include(startX * innerRatio, startY * innerRatio);
        Include(endX * innerRatio, endY * innerRatio);
        for (var k = 0; k < 4; k++)
        {
            var angle = k * 90d;
            if (Normalize(angle - startAngle) <= sweepAngle)
            {
                var (x, y) = GetDirection(angle);
                Include(x, y);
            }
        }

        return (left, top, right, bottom);
    }

    // A line along a circle, drawn with a stroke
    public static string Arc(double cx, double cy, double radius, double startAngle, double sweep)
    {
        var builder = new StringBuilder(48);
        if (sweep >= FullCircle - Tolerance)
        {
            AppendCircle(builder, cx, cy, radius, startAngle, true);
            return builder.ToString();
        }

        var (x1, y1) = GetPoint(cx, cy, radius, startAngle);
        var (x2, y2) = GetPoint(cx, cy, radius, startAngle + sweep);
        builder.Append('M').Append(Text(x1)).Append(' ').Append(Text(y1));
        AppendArc(builder, radius, sweep > 180, true, x2, y2);
        return builder.ToString();
    }

    // A slice of a pie or a donut, with parallel gaps along its two straight edges
    public static string Slice(double cx, double cy, double outer, double inner, double startAngle, double sweep, double startGap, double endGap)
    {
        var builder = new StringBuilder(96);
        if (sweep >= FullCircle - Tolerance)
        {
            AppendCircle(builder, cx, cy, outer, startAngle, true);
            if (inner > 0)
            {
                AppendCircle(builder, cx, cy, inner, startAngle, false);
            }

            return builder.ToString();
        }

        var endAngle = startAngle + sweep;
        var outerStart = startAngle + GetGapAngle(startGap, outer);
        var outerEnd = endAngle - GetGapAngle(endGap, outer);
        if (outerEnd - outerStart <= Tolerance)
        {
            return string.Empty;
        }

        var (x1, y1) = GetPoint(cx, cy, outer, outerStart);
        var (x2, y2) = GetPoint(cx, cy, outer, outerEnd);
        builder.Append('M').Append(Text(x1)).Append(' ').Append(Text(y1));
        AppendArc(builder, outer, outerEnd - outerStart > 180, true, x2, y2);

        var innerStart = inner > 0 ? startAngle + GetGapAngle(startGap, inner) : 0;
        var innerEnd = inner > 0 ? endAngle - GetGapAngle(endGap, inner) : 0;
        if ((inner > 0) && (innerEnd - innerStart > Tolerance))
        {
            var (x3, y3) = GetPoint(cx, cy, inner, innerEnd);
            var (x4, y4) = GetPoint(cx, cy, inner, innerStart);
            builder.Append('L').Append(Text(x3)).Append(' ').Append(Text(y3));
            AppendArc(builder, inner, innerEnd - innerStart > 180, false, x4, y4);
        }
        else
        {
            var (x3, y3) = GetApex(startAngle, endAngle, startGap, endGap);
            if (Math.Sqrt((x3 * x3) + (y3 * y3)) >= outer)
            {
                return string.Empty;
            }

            builder.Append('L').Append(Text(cx + x3)).Append(' ').Append(Text(cy + y3));
        }

        builder.Append('Z');
        return builder.ToString();
    }

    // A wedge from the center, used as the area that reacts to the pointer
    public static string Sector(double cx, double cy, double radius, double startAngle, double sweep)
    {
        var builder = new StringBuilder(64);
        var (x1, y1) = GetPoint(cx, cy, radius, startAngle);
        var (x2, y2) = GetPoint(cx, cy, radius, startAngle + sweep);
        builder.Append('M').Append(Text(cx)).Append(' ').Append(Text(cy));
        builder.Append('L').Append(Text(x1)).Append(' ').Append(Text(y1));
        AppendArc(builder, radius, sweep > 180, true, x2, y2);
        builder.Append('Z');
        return builder.ToString();
    }

    public static (double X, double Y) GetPoint(double cx, double cy, double radius, double clockAngle)
    {
        var (x, y) = GetDirection(clockAngle);
        return (cx + (x * radius), cy + (y * radius));
    }

    public static string Text(double value)
    {
        var rounded = Math.Round(value, 2);
        return (rounded == 0 ? 0 : rounded).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double GetGapAngle(double gap, double radius) =>
        (gap > 0) && (radius > 0) ? Math.Asin(Math.Min(1, gap / radius)) * 180 / Math.PI : 0;

    // The point where the two edges meet after each of them moved by its gap
    private static (double X, double Y) GetApex(double startAngle, double endAngle, double startGap, double endGap)
    {
        var (startX, startY) = GetDirection(startAngle + 90);
        var (endX, endY) = GetDirection(endAngle - 90);
        var determinant = (startX * endY) - (startY * endX);
        if (Math.Abs(determinant) < Tolerance)
        {
            var gap = Math.Max(startGap, endGap);
            return (startX * gap, startY * gap);
        }

        return (((startGap * endY) - (endGap * startY)) / determinant, ((endGap * startX) - (startGap * endX)) / determinant);
    }

    private static void AppendCircle(StringBuilder builder, double cx, double cy, double radius, double startAngle, bool clockwise)
    {
        var (x1, y1) = GetPoint(cx, cy, radius, startAngle);
        var (x2, y2) = GetPoint(cx, cy, radius, startAngle + 180);
        builder.Append('M').Append(Text(x1)).Append(' ').Append(Text(y1));
        AppendArc(builder, radius, true, clockwise, x2, y2);
        AppendArc(builder, radius, true, clockwise, x1, y1);
        builder.Append('Z');
    }

    private static void AppendArc(StringBuilder builder, double radius, bool large, bool clockwise, double x, double y)
    {
        var text = Text(radius);
        builder.Append('A').Append(text).Append(' ').Append(text).Append(" 0 ");
        builder.Append(large ? '1' : '0').Append(' ').Append(clockwise ? '1' : '0').Append(' ');
        builder.Append(Text(x)).Append(' ').Append(Text(y));
    }
}
