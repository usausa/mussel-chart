namespace MusselChart;

using System.Globalization;

using Microsoft.AspNetCore.Components;

using MusselChart.Internal;

public sealed partial class MusselHeatmapView<TItem>
{
    private const string EmptyCellClass = "mussel-heat-empty";

    private readonly HeatmapLayout layout = new();

    private readonly List<CellView> cells = [];

    private readonly List<LabelView> labels = [];

    private readonly List<StepView> scaleSteps = [];

    private readonly List<HeatmapEntry> entries = [];

    private readonly List<ChartTipRow> tipRows = [];

    private IReadOnlyList<string>? paletteSource;

    private string[] palette = [];

    private string emptyText = string.Empty;

    [Parameter]
    [EditorRequired]
    public IReadOnlyList<TItem>? Items { get; set; }

    [Parameter]
    public Func<TItem, string?>? Row { get; set; }

    [Parameter]
    public Func<TItem, string?>? Column { get; set; }

    [Parameter]
    public Func<TItem, DateTime>? Date { get; set; }

    [Parameter]
    public Func<TItem, double>? Value { get; set; }

    [Parameter]
    public ChartHeatmapLayoutMode LayoutMode { get; set; }

    [Parameter]
    public double? Minimum { get; set; }

    [Parameter]
    public double? Maximum { get; set; }

    [Parameter]
    public IReadOnlyList<string> ColorScale { get; set; } = ChartPalettes.OrangesLight;

    [Parameter]
    public double CellGap { get; set; } = 1;

    [Parameter]
    public string? ValueFormat { get; set; }

    [Parameter]
    public Func<double, string>? ValueFormatter { get; set; }

    [Parameter]
    public bool? LegendVisible { get; set; }

    [Parameter]
    public ChartLegendPosition LegendPosition { get; set; } = ChartLegendPosition.Bottom;

    private void Prepare()
    {
        var culture = Culture ?? CultureInfo.CurrentCulture;
        if (!ReferenceEquals(paletteSource, ColorScale))
        {
            paletteSource = ColorScale;
            palette = HeatmapPalette.Build(ColorScale);
        }

        layout.Update(Width, Height, new HeatmapLayoutOptions
        {
            LayoutMode = LayoutMode,
            Minimum = Minimum,
            Maximum = Maximum,
            CellGap = CellGap,
            LegendVisible = LegendVisible ?? true,
            LegendPosition = LegendPosition,
            HasRows = Row is not null,
            ValueFormat = ValueFormat,
            ValueFormatter = ValueFormatter,
            Culture = culture
        }, ReadEntries());

        emptyText = layout.HasValues ? string.Empty : EmptyText ?? string.Empty;
        ArrangeCells();
        ArrangeLabels();
        ArrangeScale();
    }

    private List<HeatmapEntry> ReadEntries()
    {
        entries.Clear();
        if (Items is null)
        {
            return entries;
        }

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var label = (item as ChartEntry)?.Label;
            var value = Value?.Invoke(item) ?? (item as ChartEntry)?.Value ?? Double.NaN;
            entries.Add(new HeatmapEntry(Row?.Invoke(item), Column is not null ? Column(item) : label, Date?.Invoke(item), value));
        }

        return entries;
    }

    private void ArrangeCells()
    {
        cells.Clear();
        if (!(layout.CellWidth > 0) || !(layout.CellHeight > 0))
        {
            return;
        }

        var hover = TrackballMode == ChartTrackballMode.Hover;
        for (var cell = 0; cell < layout.CellCount; cell++)
        {
            if (!layout.IsPresent(cell))
            {
                continue;
            }

            var rect = layout.GetCellRect(cell);
            var value = layout.GetValue(cell);
            var finite = Double.IsFinite(value);
            var color = finite ? palette[HeatmapPalette.GetIndex(value, layout.Minimum, layout.Maximum, palette.Length)] : null;
            string? tip = null;
            if (hover)
            {
                tipRows.Clear();
                if (finite)
                {
                    tipRows.Add(new ChartTipRow(color, null, layout.FormatValue(value)));
                }

                tip = ChartTip.Create(layout.GetCellText(cell), tipRows);
            }

            cells.Add(new CellView(ChartFormat.Pixel(rect.X), ChartFormat.Pixel(rect.Y), ChartFormat.Pixel(rect.Width), ChartFormat.Pixel(rect.Height), finite ? null : EmptyCellClass, color, tip));
        }
    }

    private void ArrangeLabels()
    {
        labels.Clear();
        AddLabels(layout.RowLabels);
        AddLabels(layout.ColumnLabels);
        AddLabels(layout.ScaleLabels);
    }

    private void AddLabels(IReadOnlyList<HeatmapLabel> source)
    {
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < source.Count; i++)
        {
            var label = source[i];
            labels.Add(new LabelView(ChartFormat.Pixel(label.X), ChartFormat.Pixel(label.Y), label.Anchor, label.Text));
        }
    }

    private void ArrangeScale()
    {
        scaleSteps.Clear();
        var bounds = layout.ScaleBounds;
        if (!layout.ShowsScale || !(bounds.Width > 0) || !(bounds.Height > 0))
        {
            return;
        }

        var steps = palette.Length;
        var vertical = layout.IsScaleVertical;
        var length = vertical ? bounds.Height : bounds.Width;
        var start = vertical ? bounds.Bottom : bounds.X;
        var direction = vertical ? -1 : 1;
        var from = Math.Round(start);
        for (var i = 0; i < steps; i++)
        {
            var to = Math.Round(start + (direction * length * (i + 1) / steps));
            if (!from.Equals(to))
            {
                var low = Math.Min(from, to);
                var size = ChartFormat.Pixel(Math.Abs(to - from));
                scaleSteps.Add(vertical
                    ? new StepView(ChartFormat.Pixel(bounds.X), ChartFormat.Pixel(low), ChartFormat.Pixel(bounds.Width), size, palette[i])
                    : new StepView(ChartFormat.Pixel(low), ChartFormat.Pixel(bounds.Y), size, ChartFormat.Pixel(bounds.Height), palette[i]));
            }

            from = to;
        }
    }

    private readonly record struct CellView(string X, string Y, string Width, string Height, string? Class, string? Color, string? Tip);

    private readonly record struct LabelView(string X, string Y, string Anchor, string Text);

    private readonly record struct StepView(string X, string Y, string Width, string Height, string Color);
}
