namespace MusselChart.Internal;

using System.Globalization;
using System.Text;

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

    public static string Arc(double cx, double cy, double radius, double startAngle, double sweep)
    {
        var builder = ChartStringBuilder.Rent();
        var (x1, y1) = GetPoint(cx, cy, radius, startAngle);
        var (x2, y2) = GetPoint(cx, cy, radius, startAngle + sweep);
        if ((sweep >= FullCircle - Tolerance) || ((sweep > 180) && Coincide(x1, y1, x2, y2)))
        {
            AppendCircle(builder, cx, cy, radius, startAngle, true);
        }
        else
        {
            builder.Append('M').AppendNumber(x1).Append(' ').AppendNumber(y1);
            AppendArc(builder, radius, sweep > 180, true, x2, y2);
        }

        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    public static string Slice(double cx, double cy, double outer, double inner, double startAngle, double sweep, double startGap, double endGap)
    {
        var endAngle = startAngle + sweep;
        var outerStart = startAngle + GetGapAngle(startGap, outer);
        var outerEnd = endAngle - GetGapAngle(endGap, outer);
        var (x1, y1) = GetPoint(cx, cy, outer, outerStart);
        var (x2, y2) = GetPoint(cx, cy, outer, outerEnd);
        var innerStart = inner > 0 ? startAngle + GetGapAngle(startGap, inner) : 0;
        var innerEnd = inner > 0 ? endAngle - GetGapAngle(endGap, inner) : 0;
        var (x3, y3) = GetPoint(cx, cy, inner, innerEnd);
        var (x4, y4) = GetPoint(cx, cy, inner, innerStart);
        var closedInside = (inner > 0) && (innerEnd - innerStart > 180) && Coincide(x3, y3, x4, y4);
        if ((sweep >= FullCircle - Tolerance) || ((outerEnd - outerStart > 180) && Coincide(x1, y1, x2, y2)) || closedInside)
        {
            var ring = ChartStringBuilder.Rent();
            AppendCircle(ring, cx, cy, outer, startAngle, true);
            if (inner > 0)
            {
                AppendCircle(ring, cx, cy, inner, startAngle, false);
            }

            return ChartStringBuilder.ToStringAndReturn(ring);
        }

        if (outerEnd - outerStart <= Tolerance)
        {
            return string.Empty;
        }

        var hasHole = (inner > 0) && (innerEnd - innerStart > Tolerance);
        var (apexX, apexY) = hasHole ? default : GetApex(startAngle, endAngle, startGap, endGap);
        if (!hasHole && (Math.Sqrt((apexX * apexX) + (apexY * apexY)) >= outer))
        {
            return string.Empty;
        }

        var builder = ChartStringBuilder.Rent();
        builder.Append('M').AppendNumber(x1).Append(' ').AppendNumber(y1);
        AppendArc(builder, outer, outerEnd - outerStart > 180, true, x2, y2);
        if (hasHole)
        {
            builder.Append('L').AppendNumber(x3).Append(' ').AppendNumber(y3);
            AppendArc(builder, inner, innerEnd - innerStart > 180, false, x4, y4);
        }
        else
        {
            builder.Append('L').AppendNumber(cx + apexX).Append(' ').AppendNumber(cy + apexY);
        }

        builder.Append('Z');
        return ChartStringBuilder.ToStringAndReturn(builder);
    }

    public static string Sector(double cx, double cy, double radius, double startAngle, double sweep)
    {
        var builder = ChartStringBuilder.Rent();
        var (x1, y1) = GetPoint(cx, cy, radius, startAngle);
        var (x2, y2) = GetPoint(cx, cy, radius, startAngle + sweep);
        builder.Append('M').AppendNumber(cx).Append(' ').AppendNumber(cy);
        builder.Append('L').AppendNumber(x1).Append(' ').AppendNumber(y1);
        AppendArc(builder, radius, sweep > 180, true, x2, y2);
        builder.Append('Z');
        return ChartStringBuilder.ToStringAndReturn(builder);
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

    private static bool Coincide(double x1, double y1, double x2, double y2) =>
        (Math.Abs(Math.Round(x1, 2) - Math.Round(x2, 2)) < 0.005) && (Math.Abs(Math.Round(y1, 2) - Math.Round(y2, 2)) < 0.005);

    private static double GetGapAngle(double gap, double radius) =>
        (gap > 0) && (radius > 0) ? Math.Asin(Math.Min(1, gap / radius)) * 180 / Math.PI : 0;

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
        builder.Append('M').AppendNumber(x1).Append(' ').AppendNumber(y1);
        AppendArc(builder, radius, true, clockwise, x2, y2);
        AppendArc(builder, radius, true, clockwise, x1, y1);
        builder.Append('Z');
    }

    private static void AppendArc(StringBuilder builder, double radius, bool large, bool clockwise, double x, double y)
    {
        builder.Append('A').AppendNumber(radius).Append(' ').AppendNumber(radius).Append(" 0 ");
        builder.Append(large ? '1' : '0').Append(' ').Append(clockwise ? '1' : '0').Append(' ');
        builder.AppendNumber(x).Append(' ').AppendNumber(y);
    }
}
