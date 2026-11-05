namespace MusselChart.Internal;

using System.Globalization;

internal readonly record struct HeatmapEntry(int DataIndex, string? Row, string? Column, DateTime? Date, double Value);

internal readonly record struct HeatmapLabel(string Text, double X, double Y, string Anchor);

internal readonly record struct HeatmapRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

internal sealed record HeatmapLayoutOptions
{
    public ChartHeatmapLayoutMode LayoutMode { get; init; }

    public double? Minimum { get; init; }

    public double? Maximum { get; init; }

    public double CellGap { get; init; } = 1;

    public bool LegendVisible { get; init; } = true;

    public ChartLegendPosition LegendPosition { get; init; } = ChartLegendPosition.Bottom;

    public bool HasRows { get; init; }

    public string? ValueFormat { get; init; }

    public Func<double, string>? ValueFormatter { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
}

internal sealed class HeatmapLayout
{
    public const int DaysPerWeek = 7;

    public const double LabelFontSize = 11;

    private const double Padding = 8;

    private const int MaxWeeks = 1000;

    private const double LegendGap = 8;

    private const double LabelGap = 4;

    private const double LabelSpacing = 8;

    private const double ScaleThickness = 9;

    private const double ScaleLabelGap = 2;

    private const double ScaleLabelSpacing = 36;

    private const double ScaleVerticalSpacing = 20;

    private const double LabelWidthRatio = 1d / 3;

    private readonly Dictionary<string, int> rowIndexes = new(StringComparer.Ordinal);

    private readonly Dictionary<string, int> columnIndexes = new(StringComparer.Ordinal);

    private readonly List<string?> rowNames = [];

    private readonly List<string?> columnNames = [];

    private readonly List<HeatmapLabel> rowLabels = [];

    private readonly List<HeatmapLabel> columnLabels = [];

    private readonly List<HeatmapLabel> scaleLabels = [];

    private double[] values = [];

    private bool[] present = [];

    private int firstDay;

    private DayOfWeek firstDayOfWeek;

    private HeatmapLayoutOptions options = new();

    private HeatmapRect bounds;

    private HeatmapRect plotBounds;

    private static double LabelHeight => ChartText.LineHeight(LabelFontSize);

    public int RowCount { get; private set; }

    public int ColumnCount { get; private set; }

    public int CellCount => RowCount * ColumnCount;

    public HeatmapRect GridBounds { get; private set; }

    public HeatmapRect ScaleBounds { get; private set; }

    public double CellWidth { get; private set; }

    public double CellHeight { get; private set; }

    public double Minimum { get; private set; }

    public double Maximum { get; private set; } = 1;

    public bool HasValues { get; private set; }

    public bool ShowsScale { get; private set; }

    public bool IsScaleVertical { get; private set; }

    public DateTime CalendarStart { get; private set; }

    public IReadOnlyList<HeatmapLabel> RowLabels => rowLabels;

    public IReadOnlyList<HeatmapLabel> ColumnLabels => columnLabels;

    public IReadOnlyList<HeatmapLabel> ScaleLabels => scaleLabels;

    public double GetValue(int cell) => values[cell];

    public bool IsPresent(int cell) => present[cell];

    public int GetRow(int cell) => cell / ColumnCount;

    public int GetColumn(int cell) => cell % ColumnCount;

    public DateTime GetDate(int cell) => CalendarStart.AddDays((GetColumn(cell) * DaysPerWeek) + GetRow(cell));

    public string FormatValue(double value) => ChartFormat.Value(value, options.ValueFormat, options.ValueFormatter, options.Culture);

    public string GetCellText(int cell)
    {
        var culture = options.Culture;
        if (options.LayoutMode == ChartHeatmapLayoutMode.Calendar)
        {
            var date = GetDate(cell);
            return date.ToString("d", culture) + " (" + culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek) + ")";
        }

        var row = rowNames[GetRow(cell)];
        var column = columnNames[GetColumn(cell)];
        if (String.IsNullOrEmpty(row))
        {
            return column ?? string.Empty;
        }

        return String.IsNullOrEmpty(column) ? row : row + " " + column;
    }

    // Edges are rounded to whole pixels and each cell leaves the gap on its right and bottom
    public HeatmapRect GetCellRect(int cell)
    {
        var column = GetColumn(cell);
        var row = GetRow(cell);
        var gap = Math.Round(options.CellGap);
        var left = Math.Round(GridBounds.X + (column * CellWidth));
        var top = Math.Round(GridBounds.Y + (row * CellHeight));
        var right = Math.Max(left + 1, Math.Round(GridBounds.X + ((column + 1) * CellWidth)) - gap);
        var bottom = Math.Max(top + 1, Math.Round(GridBounds.Y + ((row + 1) * CellHeight)) - gap);
        return new HeatmapRect(left, top, right - left, bottom - top);
    }

    public void Update(double width, double height, HeatmapLayoutOptions layoutOptions, IReadOnlyList<HeatmapEntry> entries)
    {
        options = layoutOptions with { CellGap = Math.Max(layoutOptions.CellGap, 0) };
        bounds = new HeatmapRect(Padding, Padding, Math.Max(0, width - (Padding * 2)), Math.Max(0, height - (Padding * 2)));
        if (options.LayoutMode == ChartHeatmapLayoutMode.Calendar)
        {
            BuildCalendar(entries);
        }
        else
        {
            BuildMatrix(entries);
        }

        ResolveRange();
        PlaceScale();
        if (options.LayoutMode == ChartHeatmapLayoutMode.Calendar)
        {
            PlaceCalendar();
        }
        else
        {
            PlaceMatrix();
        }

        PlaceScaleLabels();
    }

    private static int GetIndex(string? name, Dictionary<string, int> indexes, List<string?> names)
    {
        var key = name ?? string.Empty;
        if (!indexes.TryGetValue(key, out var index))
        {
            index = names.Count;
            indexes.Add(key, index);
            names.Add(name);
        }

        return index;
    }

    private void Allocate(bool filled)
    {
        var size = CellCount;
        if (values.Length < size)
        {
            values = new double[size];
            present = new bool[size];
        }

        Array.Fill(values, Double.NaN, 0, size);
        Array.Fill(present, filled, 0, size);
    }

    private void Clear()
    {
        rowIndexes.Clear();
        columnIndexes.Clear();
        rowNames.Clear();
        columnNames.Clear();
        RowCount = 0;
        ColumnCount = 0;
    }

    private void BuildMatrix(IReadOnlyList<HeatmapEntry> entries)
    {
        Clear();
        foreach (var entry in entries)
        {
            GetIndex(entry.Column, columnIndexes, columnNames);
        }

        if (options.HasRows)
        {
            foreach (var entry in entries)
            {
                GetIndex(entry.Row, rowIndexes, rowNames);
            }
        }
        else if (columnNames.Count > 0)
        {
            rowNames.Add(null);
        }

        RowCount = rowNames.Count;
        ColumnCount = columnNames.Count;
        Allocate(true);
        if (CellCount == 0)
        {
            return;
        }

        var filled = new bool[CellCount];
        foreach (var entry in entries)
        {
            var column = GetIndex(entry.Column, columnIndexes, columnNames);
            var cell = ((options.HasRows ? GetIndex(entry.Row, rowIndexes, rowNames) : 0) * ColumnCount) + column;
            if (!filled[cell])
            {
                filled[cell] = true;
                values[cell] = entry.Value;
            }
        }
    }

    private void BuildCalendar(IReadOnlyList<HeatmapEntry> entries)
    {
        Clear();
        firstDayOfWeek = options.Culture.DateTimeFormat.FirstDayOfWeek;
        var first = DateTime.MaxValue;
        var last = DateTime.MinValue;
        foreach (var entry in entries)
        {
            if (entry.Date is { } value)
            {
                var date = value.Date;
                first = date < first ? date : first;
                last = date > last ? date : last;
            }
        }

        if ((first > last) || (last.Ticks < TimeSpan.TicksPerDay * DaysPerWeek * MaxWeeks))
        {
            Allocate(false);
            return;
        }

        var oldest = last.AddDays(-GetWeekOffset(last) - (DaysPerWeek * (MaxWeeks - 1)));
        CalendarStart = first < oldest ? oldest : first.AddDays(-GetWeekOffset(first));
        firstDay = Math.Max(0, (first - CalendarStart).Days);
        var end = (last - CalendarStart).Days;
        RowCount = DaysPerWeek;
        ColumnCount = (end / DaysPerWeek) + 1;
        Allocate(false);
        for (var day = firstDay; day <= end; day++)
        {
            present[GetCalendarCell(day)] = true;
        }

        var filled = new bool[CellCount];
        foreach (var entry in entries)
        {
            if (entry.Date is not { } value)
            {
                continue;
            }

            var day = (value.Date - CalendarStart).Days;
            if (day < firstDay)
            {
                continue;
            }

            var cell = GetCalendarCell(day);
            if (!filled[cell])
            {
                filled[cell] = true;
                values[cell] = entry.Value;
            }
        }

        for (var r = 0; r < DaysPerWeek; r++)
        {
            rowNames.Add(options.Culture.DateTimeFormat.GetAbbreviatedDayName(GetDayOfWeek(r)));
        }

        for (var c = 0; c < ColumnCount; c++)
        {
            columnNames.Add(null);
        }
    }

    private int GetWeekOffset(DateTime date) => ((int)date.DayOfWeek - (int)firstDayOfWeek + DaysPerWeek) % DaysPerWeek;

    private int GetCalendarCell(int day) => ((day % DaysPerWeek) * ColumnCount) + (day / DaysPerWeek);

    private DayOfWeek GetDayOfWeek(int row) => (DayOfWeek)(((int)firstDayOfWeek + row) % DaysPerWeek);

    private void ResolveRange()
    {
        var minimum = Double.NaN;
        var maximum = Double.NaN;
        var size = CellCount;
        for (var i = 0; i < size; i++)
        {
            var value = values[i];
            if (present[i] && Double.IsFinite(value))
            {
                minimum = Double.IsNaN(minimum) ? value : Math.Min(minimum, value);
                maximum = Double.IsNaN(maximum) ? value : Math.Max(maximum, value);
            }
        }

        HasValues = !Double.IsNaN(minimum);
        var fixedMinimum = options.Minimum;
        var fixedMaximum = options.Maximum;
        if ((fixedMinimum is { } low) && (fixedMaximum is { } high) && !(high > low))
        {
            fixedMinimum = null;
            fixedMaximum = null;
        }

        Minimum = fixedMinimum ?? (HasValues ? minimum : 0);
        Maximum = fixedMaximum ?? (HasValues ? maximum : 1);
    }

    private void PlaceScale()
    {
        var area = bounds;
        plotBounds = area;
        ScaleBounds = default;
        IsScaleVertical = options.LegendPosition is ChartLegendPosition.Left or ChartLegendPosition.Right;
        ShowsScale = options.LegendVisible && HasValues;
        if (!ShowsScale)
        {
            return;
        }

        if (!IsScaleVertical)
        {
            var height = ScaleThickness + ScaleLabelGap + LabelHeight;
            var top = options.LegendPosition == ChartLegendPosition.Top;
            var used = Math.Min(area.Height, height + LegendGap);
            plotBounds = new HeatmapRect(area.X, top ? area.Y + used : area.Y, area.Width, area.Height - used);
            ScaleBounds = new HeatmapRect(area.X, top ? area.Y : area.Bottom - height, area.Width, ScaleThickness);
            return;
        }

        var width = ScaleThickness + LabelGap + Math.Max(ChartText.Measure(FormatValue(Minimum), LabelFontSize), ChartText.Measure(FormatValue(Maximum), LabelFontSize));
        if (area.Width - width - LegendGap < width * 2)
        {
            ShowsScale = false;
            return;
        }

        var left = options.LegendPosition == ChartLegendPosition.Left;
        plotBounds = new HeatmapRect(left ? area.X + width + LegendGap : area.X, area.Y, area.Width - width - LegendGap, area.Height);
        ScaleBounds = new HeatmapRect(left ? area.X : area.Right - width, area.Y, ScaleThickness, area.Height);
    }

    private void PlaceMatrix()
    {
        rowLabels.Clear();
        columnLabels.Clear();
        GridBounds = default;
        CellWidth = 0;
        CellHeight = 0;
        if (CellCount == 0)
        {
            return;
        }

        var area = plotBounds;
        var maxWidth = bounds.Width * LabelWidthRatio;
        var rowWidth = 0d;
        foreach (var name in rowNames)
        {
            if (name is not null)
            {
                rowWidth = Math.Max(rowWidth, Math.Min(ChartText.Measure(name, LabelFontSize), maxWidth));
            }
        }

        var named = columnNames.Exists(static x => !String.IsNullOrEmpty(x));
        var left = area.X + (rowWidth > 0 ? rowWidth + LabelGap : 0);
        var bottom = area.Bottom - (named ? LabelHeight + LabelGap : 0);
        CellWidth = Math.Max(0, area.Right - left) / ColumnCount;
        CellHeight = Math.Max(0, bottom - area.Y) / RowCount;
        GridBounds = new HeatmapRect(left, area.Y, CellWidth * ColumnCount, CellHeight * RowCount);
        if (rowWidth > 0)
        {
            PlaceRowLabels(maxWidth);
        }

        if (named)
        {
            PlaceColumnLabels();
        }
    }

    private void PlaceRowLabels(double maxWidth)
    {
        var candidates = new List<HeatmapLabel>();
        var extents = new List<LabelExtent>();
        for (var r = 0; r < RowCount; r++)
        {
            if (rowNames[r] is { } name)
            {
                var center = GridBounds.Y + (r * CellHeight) + ((CellHeight - options.CellGap) / 2);
                candidates.Add(new HeatmapLabel(ChartText.Truncate(name, LabelFontSize, maxWidth), GridBounds.X - LabelGap, center, "end"));
                extents.Add(new LabelExtent(center - (LabelHeight / 2), center + (LabelHeight / 2)));
            }
        }

        SelectLabels(candidates, extents, rowLabels);
    }

    private void PlaceColumnLabels()
    {
        var candidates = new List<HeatmapLabel>();
        var extents = new List<LabelExtent>();
        for (var c = 0; c < ColumnCount; c++)
        {
            if (columnNames[c] is not { } name)
            {
                continue;
            }

            var labelWidth = ChartText.Measure(name, LabelFontSize);
            var center = GridBounds.X + (c * CellWidth) + ((CellWidth - options.CellGap) / 2);
            var x = Math.Clamp(center - (labelWidth / 2), bounds.X, Math.Max(bounds.X, bounds.Right - labelWidth));
            candidates.Add(new HeatmapLabel(name, x + (labelWidth / 2), GridBounds.Bottom + LabelGap + (LabelHeight / 2), "middle"));
            extents.Add(new LabelExtent(x, x + labelWidth));
        }

        SelectLabels(candidates, extents, columnLabels);
    }

    private static void SelectLabels(List<HeatmapLabel> candidates, List<LabelExtent> extents, List<HeatmapLabel> destination)
    {
        var selected = new List<int>();
        LabelThinning.Select(extents, LabelSpacing, Int32.MaxValue, selected);
        foreach (var index in selected)
        {
            destination.Add(candidates[index]);
        }
    }

    private void PlaceCalendar()
    {
        rowLabels.Clear();
        columnLabels.Clear();
        GridBounds = default;
        CellWidth = 0;
        CellHeight = 0;
        if (CellCount == 0)
        {
            return;
        }

        var area = plotBounds;
        var weekdayWidth = 0d;
        for (var r = 0; r < DaysPerWeek; r++)
        {
            if (IsLabeledDay(r))
            {
                weekdayWidth = Math.Max(weekdayWidth, ChartText.Measure(rowNames[r], LabelFontSize));
            }
        }

        var labelsWidth = weekdayWidth > 0 ? weekdayWidth + LabelGap : 0;
        var monthHeight = LabelHeight + LabelGap;
        var pitch = Math.Max(0, Math.Min((area.Width - labelsWidth) / ColumnCount, (area.Height - monthHeight) / DaysPerWeek));
        var blockWidth = labelsWidth + (pitch * ColumnCount);
        var blockHeight = monthHeight + (pitch * DaysPerWeek);
        var x = area.X + Math.Max(0, (area.Width - blockWidth) / 2);
        var y = area.Y + Math.Max(0, (area.Height - blockHeight) / 2);
        if (ShowsScale && IsScaleVertical)
        {
            var reserved = bounds.Width - area.Width;
            var start = bounds.X + Math.Max(0, (bounds.Width - blockWidth - reserved) / 2);
            var left = ScaleBounds.X < area.X;
            x = left ? start + reserved : start;
            ScaleBounds = ScaleBounds with { X = left ? start : x + blockWidth + LegendGap };
        }
        else if (ShowsScale)
        {
            var reserved = bounds.Height - area.Height;
            var start = bounds.Y + Math.Max(0, (bounds.Height - blockHeight - reserved) / 2);
            var top = ScaleBounds.Y < area.Y;
            y = top ? start + reserved : start;
            ScaleBounds = ScaleBounds with { Y = top ? start : y + blockHeight + LegendGap };
        }

        CellWidth = pitch;
        CellHeight = pitch;
        GridBounds = new HeatmapRect(x + labelsWidth, y + monthHeight, pitch * ColumnCount, pitch * DaysPerWeek);
        if ((weekdayWidth > 0) && (pitch * 2 >= LabelHeight))
        {
            for (var r = 0; r < DaysPerWeek; r++)
            {
                if (IsLabeledDay(r))
                {
                    var center = GridBounds.Y + (r * pitch) + ((pitch - options.CellGap) / 2);
                    rowLabels.Add(new HeatmapLabel(rowNames[r]!, GridBounds.X - LabelGap, center, "end"));
                }
            }
        }

        PlaceMonthLabels(y);
    }

    private bool IsLabeledDay(int row) => GetDayOfWeek(row) is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;

    private void PlaceMonthLabels(double top)
    {
        var candidates = new List<(string Text, double X, double Width)>();
        var previous = -1;
        for (var c = 0; c < ColumnCount; c++)
        {
            var date = CalendarStart.AddDays(Math.Max(c * DaysPerWeek, firstDay));
            var month = (date.Year * 12) + date.Month;
            if (month == previous)
            {
                continue;
            }

            previous = month;
            var text = options.Culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month);
            candidates.Add((text, GridBounds.X + (c * CellWidth), ChartText.Measure(text, LabelFontSize)));
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var (text, x, width) = candidates[i];
            var limit = i < candidates.Count - 1 ? candidates[i + 1].X - LabelSpacing : bounds.Right;
            if (x + width <= limit)
            {
                columnLabels.Add(new HeatmapLabel(text, x, top + (LabelHeight / 2), "start"));
            }
        }
    }

    private void PlaceScaleLabels()
    {
        scaleLabels.Clear();
        if (!ShowsScale || (CellCount == 0))
        {
            ShowsScale = false;
            return;
        }

        ScaleBounds = IsScaleVertical
            ? ScaleBounds with { Y = GridBounds.Y, Height = Math.Max(0, GridBounds.Height - options.CellGap) }
            : ScaleBounds with { X = GridBounds.X, Width = Math.Max(0, GridBounds.Width - options.CellGap) };
        var length = IsScaleVertical ? ScaleBounds.Height : ScaleBounds.Width;
        if (!(length > 0))
        {
            return;
        }

        var span = Maximum - Minimum;
        var count = 1;
        var texts = new List<string>();
        if (span > 0)
        {
            var widest = Math.Max(ChartText.Measure(FormatValue(Minimum), LabelFontSize), ChartText.Measure(FormatValue(Maximum), LabelFontSize));
            count = Math.Max(2, (int)Math.Floor(length / (IsScaleVertical ? LabelHeight + ScaleVerticalSpacing : widest + ScaleLabelSpacing)));
            while ((count > 2) && !FitsScale(count, length))
            {
                count--;
            }

            CreateScaleTexts(count, texts);
        }
        else
        {
            texts.Add(FormatValue(Minimum));
        }

        for (var i = 0; i < count; i++)
        {
            var ratio = count > 1 ? (GetScaleValue(i, count) - Minimum) / span : 0;
            var edge = i == 0 ? "start" : i == count - 1 ? "end" : "middle";
            scaleLabels.Add(IsScaleVertical
                ? new HeatmapLabel(texts[i], ScaleBounds.Right + LabelGap, ScaleBounds.Bottom - (ratio * length) - (i == 0 ? LabelHeight / 2 : i == count - 1 ? -LabelHeight / 2 : 0), "start")
                : new HeatmapLabel(texts[i], ScaleBounds.X + (ratio * length), ScaleBounds.Bottom + ScaleLabelGap + (LabelHeight / 2), edge));
        }
    }

    private double GetScaleValue(int index, int count)
    {
        var value = Minimum + ((Maximum - Minimum) * index / (count - 1));
        return Double.IsInteger(Minimum) && Double.IsInteger(Maximum) ? Math.Floor(value + 0.5) : value;
    }

    private void CreateScaleTexts(int count, List<string> texts)
    {
        texts.Clear();
        for (var i = 0; i < count; i++)
        {
            texts.Add(FormatValue(GetScaleValue(i, count)));
        }
    }

    private bool FitsScale(int count, double length)
    {
        var texts = new List<string>(count);
        CreateScaleTexts(count, texts);
        var widest = 0d;
        for (var i = 0; i < count; i++)
        {
            widest = Math.Max(widest, ChartText.Measure(texts[i], LabelFontSize));
            for (var j = 0; j < i; j++)
            {
                if (String.Equals(texts[i], texts[j], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return count * (IsScaleVertical ? LabelHeight + ScaleVerticalSpacing : widest + ScaleLabelSpacing) <= length;
    }
}
