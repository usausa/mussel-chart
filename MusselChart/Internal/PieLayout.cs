namespace MusselChart.Internal;

using System.Globalization;

internal sealed record PieLayoutOptions
{
    public double InnerRadius { get; init; } = 0.6;

    public double StartAngle { get; init; }

    public double SweepAngle { get; init; } = 360;

    public double Gap { get; init; } = 2;

    public ChartSortMode SortMode { get; init; }

    public int MaxSlices { get; init; } = 8;

    public double OthersThreshold { get; init; }

    public string OthersLabel { get; init; } = "Other";

    public ChartPieLabelsMode LabelsMode { get; init; }

    public ChartPieLabelContent LabelContent { get; init; }

    public ChartCenterValueMode CenterValueMode { get; init; }

    public double? CenterValue { get; init; }

    public string? CenterLabel { get; init; }

    public string? ValueFormat { get; init; }

    public Func<double, string>? ValueFormatter { get; init; }

    public string? CenterValueFormat { get; init; }

    public Func<double, string>? CenterValueFormatter { get; init; }

    public IReadOnlySet<string>? HiddenLabels { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

internal sealed record PieSlice(int DataIndex, double Value, double Start, double End, int Rank, string? Color, string? Label, string PercentText, string ValueText, string? LabelText, double LabelWidth)
{
    public bool IsOthers => DataIndex < 0;
}

internal readonly record struct PieOutsideLabel(PieSlice Slice, double X, double Y, double EdgeX, double EdgeY, double ElbowX, double ElbowY, double EndX, bool Right);

internal sealed record PieCenterText(string Value, string? Caption, double ValueFontSize, double CaptionFontSize, double ValueY, double CaptionY);

internal sealed class PieLayout
{
    public const double DataLabelFontSize = 11;

    private const double Padding = 8;

    private const double LabelRadius = 0.62;

    private const double LabelPadding = 2;

    private const double LegendFontSize = 12;

    private const double CenterPadding = 4;

    private const double CaptionGap = 2;

    private const double MaxCenterFontSize = 32;

    private const double MinCenterFontSize = 6;

    private const double CenterFontRatio = 0.5;

    private const double CaptionFontRatio = 0.45;

    private const double OutsideLineLength = 8;

    private const double OutsideLineRun = 8;

    private const double OutsideTextGap = 4;

    private const double OutsideLabelGap = 2;

    private const double MinOutsideRadiusRatio = 0.5;

    private const double MinExtent = 1e-6;

    private const double Tolerance = 1e-6;

    private const int FitAttempts = 3;

    private const string HiddenPercentText = "-";

    private readonly List<PieSlice> slices = [];

    private readonly List<PieSlice> hiddenSlices = [];

    private readonly List<PieSlice> legendSlices = [];

    private readonly List<PieOutsideLabel> outsideLabels = [];

    private PieLayoutOptions options = new();

    private (double X, double Y, double Width, double Height) area;

    public IReadOnlyList<PieSlice> Slices => slices;

    public IReadOnlyList<PieSlice> LegendSlices => legendSlices;

    public IReadOnlyList<PieOutsideLabel> OutsideLabels => outsideLabels;

    public double CenterX { get; private set; }

    public double CenterY { get; private set; }

    public double OuterRadius { get; private set; }

    public double InnerRadius { get; private set; }

    public double Total { get; private set; }

    public PieCenterText? Center { get; private set; }

    private double StartAngle => options.StartAngle;

    private double SweepAngle => options.SweepAngle;

    private static double LabelHeight => ChartText.LineHeight(DataLabelFontSize);

    public void Update(double width, double height, PieLayoutOptions layoutOptions, IReadOnlyList<ChartEntry> items)
    {
        options = layoutOptions with
        {
            InnerRadius = Math.Clamp(layoutOptions.InnerRadius, 0, 0.95),
            SweepAngle = layoutOptions.SweepAngle > 0 ? Math.Min(layoutOptions.SweepAngle, 360) : 360,
            MaxSlices = Math.Max(layoutOptions.MaxSlices, 1),
            Gap = Math.Max(layoutOptions.Gap, 0)
        };
        area = (Padding, Padding, Math.Max(0, width - (Padding * 2)), Math.Max(0, height - (Padding * 2)));
        BuildSlices(items);
        BuildLegend();
        PlacePie();
        PlaceOutsideLabels();
        PrepareCenter();
    }

    public double GetAngle(double fraction) => StartAngle + (SweepAngle * fraction);

    public string GetPath(PieSlice slice)
    {
        var circle = SweepAngle >= 360;
        var full = circle && (slice.End - slice.Start >= 1 - Tolerance);
        var half = options.Gap / 2;
        var startGap = !full && ((slice.Start > Tolerance) || circle) ? half : 0;
        var endGap = !full && ((slice.End < 1 - Tolerance) || circle) ? half : 0;
        return ChartGeometry.Slice(CenterX, CenterY, OuterRadius, InnerRadius, GetAngle(slice.Start), SweepAngle * (slice.End - slice.Start), startGap, endGap);
    }

    // The label sits in the middle of the ring or at 0.62 of the radius of a pie, and only when it fits in its slice
    public (double X, double Y)? GetInsideLabel(PieSlice slice)
    {
        if ((slice.LabelText is null) || (OuterRadius <= 0))
        {
            return null;
        }

        var radius = InnerRadius > 0 ? (OuterRadius + InnerRadius) / 2 : OuterRadius * LabelRadius;
        var (x, y) = ChartGeometry.GetPoint(CenterX, CenterY, radius, GetAngle((slice.Start + slice.End) / 2));
        return FitsSlice(slice, x, y) ? (x, y) : null;
    }

    private bool FitsSlice(PieSlice slice, double x, double y)
    {
        var startAngle = GetAngle(slice.Start);
        var sweep = SweepAngle * (slice.End - slice.Start);
        var left = x - (slice.LabelWidth / 2);
        var right = x + (slice.LabelWidth / 2);
        var top = y - (LabelHeight / 2);
        var bottom = y + (LabelHeight / 2);
        var nearestX = Math.Clamp(CenterX, left, right);
        var nearestY = Math.Clamp(CenterY, top, bottom);
        if (Math.Sqrt(Square(nearestX - CenterX) + Square(nearestY - CenterY)) < InnerRadius + LabelPadding)
        {
            return false;
        }

        var clearance = (options.Gap / 2) + LabelPadding;
        return IsInside(left, top, startAngle, sweep, clearance) && IsInside(right, top, startAngle, sweep, clearance) && IsInside(left, bottom, startAngle, sweep, clearance) && IsInside(right, bottom, startAngle, sweep, clearance);
    }

    private bool IsInside(double x, double y, double startAngle, double sweep, double clearance)
    {
        var dx = x - CenterX;
        var dy = y - CenterY;
        if (Math.Sqrt(Square(dx) + Square(dy)) > OuterRadius - LabelPadding)
        {
            return false;
        }

        if (sweep >= 360 - Tolerance)
        {
            return true;
        }

        if (ChartGeometry.Normalize(ChartGeometry.GetClockAngle(dx, dy) - startAngle) > sweep)
        {
            return false;
        }

        return (GetRayDistance(dx, dy, startAngle) >= clearance) && (GetRayDistance(dx, dy, startAngle + sweep) >= clearance);
    }

    private static double GetRayDistance(double dx, double dy, double clockAngle)
    {
        var (x, y) = ChartGeometry.GetDirection(clockAngle);
        var along = (dx * x) + (dy * y);
        return along <= 0 ? Math.Sqrt(Square(dx) + Square(dy)) : Math.Abs((dx * y) - (dy * x));
    }

    private static double Square(double value) => value * value;

    private static double RoundSize(double size) => Math.Floor(size * 2) / 2;

    private static int LargerFirst(PieCandidate x, PieCandidate y)
    {
        var value = y.Value.CompareTo(x.Value);
        return value != 0 ? value : x.Index.CompareTo(y.Index);
    }

    private static int SmallerFirst(PieCandidate x, PieCandidate y)
    {
        var value = x.Value.CompareTo(y.Value);
        return value != 0 ? value : x.Index.CompareTo(y.Index);
    }

    private static int DataOrder(PieCandidate x, PieCandidate y) => x.Index.CompareTo(y.Index);

    private int CountKept(List<PieCandidate> sorted, double total)
    {
        var small = 0;
        if (options.OthersThreshold > 0)
        {
            for (var k = sorted.Count - 1; (k >= 0) && (sorted[k].Value / total < options.OthersThreshold); k--)
            {
                small++;
            }
        }

        var kept = small >= 2 ? sorted.Count - small : sorted.Count;
        var count = kept + (kept < sorted.Count ? 1 : 0);
        return count > options.MaxSlices ? options.MaxSlices - 1 : kept;
    }

    private bool IsHidden(string? label) =>
        (options.HiddenLabels is { Count: > 0 } labels) && !String.IsNullOrEmpty(label) && labels.Contains(label);

    private void BuildSlices(IReadOnlyList<ChartEntry> items)
    {
        slices.Clear();
        hiddenSlices.Clear();
        var shown = new List<PieCandidate>();
        var hidden = new List<PieCandidate>();
        for (var i = 0; i < items.Count; i++)
        {
            var value = items[i].Value;
            if (Double.IsFinite(value) && (value > 0))
            {
                (IsHidden(items[i].Label) ? hidden : shown).Add(new PieCandidate(value, i));
            }
        }

        if (shown.Count == 0)
        {
            shown.AddRange(hidden);
            hidden.Clear();
        }

        var total = shown.Sum(static x => x.Value);
        Total = Double.IsFinite(total) ? total : 0;
        if ((shown.Count == 0) || (Total <= 0))
        {
            Total = 0;
            return;
        }

        var ranks = RankByData(items.Count, shown, hidden);
        shown.Sort(LargerFirst);
        var kept = CountKept(shown, total);
        var drawn = shown.GetRange(0, kept);
        var merged = shown.GetRange(kept, shown.Count - kept);
        drawn.Sort(DataOrder);
        hidden.Sort(DataOrder);
        Sort(drawn);
        Sort(hidden);

        foreach (var item in hidden)
        {
            var text = ChartFormat.Value(item.Value, options.ValueFormat, options.ValueFormatter, options.Culture);
            hiddenSlices.Add(new PieSlice(item.Index, item.Value, 0, 0, ranks[item.Index], items[item.Index].Color, items[item.Index].Label, HiddenPercentText, text, null, 0));
        }

        var cumulative = 0d;
        for (var k = 0; k < drawn.Count; k++)
        {
            var item = drawn[k];
            var start = cumulative / total;
            cumulative += item.Value;
            var end = (k == drawn.Count - 1) && (merged.Count == 0) ? 1 : Math.Min(1, cumulative / total);
            slices.Add(CreateSlice(item.Index, item.Value, start, end, ranks[item.Index], items[item.Index].Color, items[item.Index].Label, total));
        }

        if (merged.Count > 0)
        {
            slices.Add(CreateSlice(-1, merged.Sum(static x => x.Value), Math.Min(1, cumulative / total), 1, -1, null, options.OthersLabel, total));
        }
    }

    // Colors follow the data order of the slices drawn when nothing is hidden, followed by what hiding brings out of the merged slice
    private int[] RankByData(int count, List<PieCandidate> shown, List<PieCandidate> hidden)
    {
        var ranks = new int[count];
        Array.Fill(ranks, -1);
        var all = new List<PieCandidate>(shown.Count + hidden.Count);
        all.AddRange(shown);
        all.AddRange(hidden);
        var total = all.Sum(static x => x.Value);
        all.Sort(LargerFirst);
        var kept = CountKept(all, total);
        var first = all.GetRange(0, kept);
        var rest = all.GetRange(kept, all.Count - kept);
        first.Sort(DataOrder);
        rest.Sort(DataOrder);
        var rank = 0;
        foreach (var item in first)
        {
            ranks[item.Index] = rank++;
        }

        foreach (var item in rest)
        {
            ranks[item.Index] = rank++;
        }

        return ranks;
    }

    private void Sort(List<PieCandidate> items)
    {
        if (options.SortMode == ChartSortMode.Descending)
        {
            items.Sort(LargerFirst);
        }
        else if (options.SortMode == ChartSortMode.Ascending)
        {
            items.Sort(SmallerFirst);
        }
    }

    private PieSlice CreateSlice(int index, double value, double start, double end, int rank, string? color, string? label, double total)
    {
        var percent = (value / total).ToString("P0", options.Culture);
        var text = ChartFormat.Value(value, options.ValueFormat, options.ValueFormatter, options.Culture);
        var labelText = options.LabelsMode != ChartPieLabelsMode.None ? GetLabelText(label, percent, text) : null;
        return new PieSlice(index, value, start, end, rank, color, label, percent, text, labelText, ChartText.Measure(labelText, DataLabelFontSize));
    }

    private string? GetLabelText(string? label, string percent, string value) => options.LabelContent switch
    {
        ChartPieLabelContent.Value => value,
        ChartPieLabelContent.Label => String.IsNullOrEmpty(label) ? null : label,
        ChartPieLabelContent.LabelAndPercent => String.IsNullOrEmpty(label) ? percent : label + " " + percent,
        _ => percent
    };

    private bool Precedes(PieSlice hidden, PieSlice slice) => options.SortMode switch
    {
        ChartSortMode.Descending => (hidden.Value > slice.Value) || (hidden.Value.Equals(slice.Value) && (hidden.DataIndex < slice.DataIndex)),
        ChartSortMode.Ascending => (hidden.Value < slice.Value) || (hidden.Value.Equals(slice.Value) && (hidden.DataIndex < slice.DataIndex)),
        _ => hidden.DataIndex < slice.DataIndex
    };

    private void BuildLegend()
    {
        legendSlices.Clear();
        var next = 0;
        PieSlice? others = null;
        foreach (var slice in slices)
        {
            if (slice.IsOthers)
            {
                others = slice;
                continue;
            }

            for (; (next < hiddenSlices.Count) && Precedes(hiddenSlices[next], slice); next++)
            {
                legendSlices.Add(hiddenSlices[next]);
            }

            legendSlices.Add(slice);
        }

        for (; next < hiddenSlices.Count; next++)
        {
            legendSlices.Add(hiddenSlices[next]);
        }

        if (others is not null)
        {
            legendSlices.Add(others);
        }
    }

    public bool IsHiddenSlice(PieSlice slice) => hiddenSlices.Contains(slice);

    private bool IsRightSide(PieSlice slice) => ChartGeometry.GetDirection(GetAngle((slice.Start + slice.End) / 2)).X >= 0;

    private void PlacePie()
    {
        var (left, top, right, bottom) = ChartGeometry.GetUnitBounds(StartAngle, SweepAngle, options.InnerRadius);
        var radius = Math.Max(0, Math.Min(area.Width / Math.Max(right - left, MinExtent), area.Height / Math.Max(bottom - top, MinExtent)));
        if ((options.LabelsMode == ChartPieLabelsMode.Outside) && TryPlaceWithOutsideLabels(left, top, right, bottom, radius))
        {
            return;
        }

        OuterRadius = radius;
        InnerRadius = radius * options.InnerRadius;
        CenterX = area.X + (area.Width / 2) - (radius * (left + right) / 2);
        CenterY = area.Y + (area.Height / 2) - (radius * (top + bottom) / 2);
    }

    private bool TryPlaceWithOutsideLabels(double unitLeft, double unitTop, double unitRight, double unitBottom, double radius)
    {
        var reach = OutsideLineLength + OutsideLineRun + OutsideTextGap;
        var vertical = OutsideLineLength + (LabelHeight / 2);
        var height = Math.Max(unitBottom - unitTop, MinExtent);
        var limit = Double.PositiveInfinity;
        while (true)
        {
            var (leftWidth, rightWidth) = MeasureOutsideLabels(limit);
            if ((leftWidth <= 0) && (rightWidth <= 0))
            {
                return false;
            }

            var leftReach = leftWidth > 0 ? reach + leftWidth : 0;
            var rightReach = rightWidth > 0 ? reach + rightWidth : 0;
            var leftUnits = leftWidth > 0 ? 1 : -unitLeft;
            var rightUnits = rightWidth > 0 ? 1 : unitRight;
            var fitted = Math.Min((area.Width - leftReach - rightReach) / Math.Max(leftUnits + rightUnits, MinExtent), (area.Height - (vertical * 2)) / height);
            if (fitted >= radius * MinOutsideRadiusRatio)
            {
                OuterRadius = fitted;
                InnerRadius = fitted * options.InnerRadius;
                CenterX = area.X + ((area.Width - (fitted * (leftUnits + rightUnits)) - leftReach - rightReach) / 2) + (fitted * leftUnits) + leftReach;
                CenterY = area.Y + ((area.Height - (fitted * height) - (vertical * 2)) / 2) + vertical - (fitted * unitTop);
                return true;
            }

            limit = Math.Max(leftWidth, rightWidth);
        }
    }

    private (double Left, double Right) MeasureOutsideLabels(double limit)
    {
        var left = 0d;
        var right = 0d;
        foreach (var slice in slices)
        {
            if ((slice.LabelText is null) || (slice.LabelWidth >= limit))
            {
                continue;
            }

            if (IsRightSide(slice))
            {
                right = Math.Max(right, slice.LabelWidth);
            }
            else
            {
                left = Math.Max(left, slice.LabelWidth);
            }
        }

        return (left, right);
    }

    private void PlaceOutsideLabels()
    {
        outsideLabels.Clear();
        if ((options.LabelsMode != ChartPieLabelsMode.Outside) || (OuterRadius <= 0))
        {
            return;
        }

        PlaceOutsideSide(true);
        PlaceOutsideSide(false);
    }

    private void PlaceOutsideSide(bool right)
    {
        var reach = OuterRadius + OutsideLineLength;
        var end = right ? CenterX + reach + OutsideLineRun : CenterX - reach - OutsideLineRun;
        var half = LabelHeight / 2;
        var candidates = new List<OutsideCandidate>();
        foreach (var slice in slices)
        {
            if ((slice.LabelText is null) || (IsRightSide(slice) != right))
            {
                continue;
            }

            var x = right ? end + OutsideTextGap : end - OutsideTextGap - slice.LabelWidth;
            if ((x < area.X - Tolerance) || (x + slice.LabelWidth > area.X + area.Width + Tolerance))
            {
                continue;
            }

            var (_, y) = ChartGeometry.GetDirection(GetAngle((slice.Start + slice.End) / 2));
            var desired = CenterY + (reach * y) - half;
            candidates.Add(new OutsideCandidate(slice, desired, desired));
        }

        candidates.Sort(static (x, y) =>
        {
            var value = x.Desired.CompareTo(y.Desired);
            return value != 0 ? value : x.Slice.Start.CompareTo(y.Slice.Start);
        });
        while (!SpreadOutsideLabels(candidates))
        {
            RemoveSmallest(candidates);
        }

        foreach (var candidate in candidates)
        {
            var slice = candidate.Slice;
            var middle = candidate.Top + half;
            var rise = middle - CenterY;
            var spread = Math.Sqrt(Math.Max(0, (reach * reach) - (rise * rise)));
            var (edgeX, edgeY) = ChartGeometry.GetPoint(CenterX, CenterY, OuterRadius, GetAngle((slice.Start + slice.End) / 2));
            var textX = right ? end + OutsideTextGap : end - OutsideTextGap;
            outsideLabels.Add(new PieOutsideLabel(slice, textX, middle, edgeX, edgeY, right ? CenterX + spread : CenterX - spread, middle, end, right));
        }
    }

    private bool SpreadOutsideLabels(List<OutsideCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return true;
        }

        var next = area.Y;
        for (var k = 0; k < candidates.Count; k++)
        {
            var top = Math.Max(candidates[k].Desired, next);
            candidates[k] = candidates[k] with { Top = top };
            next = top + LabelHeight + OutsideLabelGap;
        }

        var limit = area.Y + area.Height;
        for (var k = candidates.Count - 1; k >= 0; k--)
        {
            var top = Math.Min(candidates[k].Top, limit - LabelHeight);
            candidates[k] = candidates[k] with { Top = top };
            limit = top - OutsideLabelGap;
        }

        return candidates[0].Top >= area.Y - Tolerance;
    }

    private static void RemoveSmallest(List<OutsideCandidate> candidates)
    {
        var smallest = 0;
        for (var k = 1; k < candidates.Count; k++)
        {
            if (candidates[k].Slice.Value <= candidates[smallest].Slice.Value)
            {
                smallest = k;
            }
        }

        candidates.RemoveAt(smallest);
    }

    private void PrepareCenter()
    {
        Center = null;
        if ((options.CenterValueMode == ChartCenterValueMode.None) || (InnerRadius <= 0) || (slices.Count == 0))
        {
            return;
        }

        Center = FitCenter(options.CenterValue ?? Total, options.CenterLabel);
    }

    // The value takes half of the hole, and shrinks until the value and the caption fit inside it
    private PieCenterText? FitCenter(double number, string? caption)
    {
        var format = options.CenterValueFormat ?? options.ValueFormat;
        var formatter = options.CenterValueFormatter ?? options.ValueFormatter;
        var value = ChartFormat.Value(number, format, formatter, options.Culture);
        var available = InnerRadius - CenterPadding;
        var valueSize = RoundSize(Math.Min(MaxCenterFontSize, InnerRadius * CenterFontRatio));
        for (var attempt = 0; attempt < FitAttempts; attempt++)
        {
            if ((available <= 0) || (valueSize < MinCenterFontSize))
            {
                return null;
            }

            var captionSize = RoundSize(Math.Min(LegendFontSize, valueSize * CaptionFontRatio));
            var text = !String.IsNullOrEmpty(caption) && (captionSize >= MinCenterFontSize) ? caption : null;
            var valueWidth = ChartText.Measure(value, valueSize);
            var valueHeight = ChartText.LineHeight(valueSize);
            var captionWidth = ChartText.Measure(text, captionSize);
            var captionHeight = text is null ? 0 : ChartText.LineHeight(captionSize);
            var height = valueHeight + (text is null ? 0 : CaptionGap + captionHeight);
            var width = Math.Max(valueWidth, captionWidth);
            var top = Math.Clamp(CenterY - (height / 2), area.Y, Math.Max(area.Y, area.Y + area.Height - height));
            var reach = Math.Max(Math.Abs(top - CenterY), Math.Abs(top + height - CenterY));
            var extent = Math.Sqrt((width * width / 4) + (reach * reach));
            if (extent <= available)
            {
                return new PieCenterText(value, text, valueSize, captionSize, top + (valueHeight / 2), top + valueHeight + CaptionGap + (captionHeight / 2));
            }

            valueSize = RoundSize(valueSize * available / extent);
        }

        return null;
    }

    private readonly record struct PieCandidate(double Value, int Index);

    private readonly record struct OutsideCandidate(PieSlice Slice, double Desired, double Top);
}
