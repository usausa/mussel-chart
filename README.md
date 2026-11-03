# MusselChart - Charts for Blazor

[![NuGet](https://img.shields.io/nuget/v/MusselChart.svg)](https://www.nuget.org/packages/MusselChart/)

## 🐚 What is this?

Charts for Blazor Server and Blazor WebAssembly drawn as SVG by Razor, made for dashboards and widgets.  
Lines, areas, bars, KPI tiles, sparklines and bar gauges follow the names and the look of [ClamChart](https://github.com/usausa/clam-chart) for .NET MAUI.  
A page needs no JavaScript library; a small module of the package measures the width and draws the tooltip in the browser.  

| 🖥️ Monitoring board | 📈 Charts | 🔢 Widgets |
|:-:|:-:|:-:|
| <img src="Document/monitor.png" width="240" /> | <img src="Document/charts.png" width="240" /> | <img src="Document/widgets.png" width="240" /> |

## 🚀 Quick Start

Add the package and its namespace.  

```razor
@using MusselChart
```

The styles of the components come with the `{App}.styles.css` of the application.  
Link `musselchart.css` only when the dark values are used.  

```html
<link rel="stylesheet" href="_content/MusselChart/musselchart.css" />
```

Write the axes and the series inside a chart, with typed delegates for X and Y.  

```razor
<MusselChartView Height="260"
                 LegendMode="ChartLegendMode.Table"
                 LegendValues="ChartLegendValues.Last | ChartLegendValues.Max"
                 TrackingGroup="monitor">
    <DateTimeAxis WindowSize="TimeSpan.FromMinutes(3)" />
    <NumericAxis Minimum="0" Maximum="100" />
    <LineSeries Items="Samples" X="static x => x.Time" Y="static x => x.Cpu" Title="CPU" ValueFormat="{0:N1}%" />
    <AreaSeries Items="Samples" X="static x => x.Time" Y="static x => x.Io" Title="IO" FillOpacity="0.4" />
</MusselChartView>

<MusselStatView Title="CPU" Value="Cpu" Unit="%" ValueFormat="N1" TrendValues="CpuTrend"
                ColorMode="ChartStatColorMode.Background" FillMode="ChartFillMode.Gradient" Fill="#5794F2"
                TrendPlacement="ChartTrendPlacement.Background" />
```

## ⚡ Real-time

Give real-time data a `ChartRingBuffer<T>`.  
A background service pushes each item once, every page reads the same buffer, and each chart draws only the items added since its last render.  

```csharp
public ChartRingBuffer<Sample> Samples { get; } = new(190);

Samples.Push(new Sample(DateTime.UtcNow, cpu, io));
```

Points are kept in data coordinates and moved by a transform, and lines and areas are cut into keyed pieces of 32 points, so a new point sends only the last piece and the transform over the circuit of Blazor Server.  
A board of four tiles, three charts and a gauge updated every second sends about 10 KB a second to a viewer before compression.  
Other lists are read again on every render, so keep them for data that changes rarely.  

## 🎨 Look

The colors are CSS variables with light defaults; set them on any container to restyle the charts inside it.  
`musselchart.css` adds the class `mussel-dark` with the dark values.  

| Variable | Use |
|---|---|
| `--mussel-primary-text`, `--mussel-secondary-text`, `--mussel-muted-text` | Text, axis labels and empty text |
| `--mussel-grid-line`, `--mussel-track` | Grid lines, legend lines and the tracks of gauges |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | Tooltip |
| `--mussel-good`, `--mussel-warning`, `--mussel-serious`, `--mussel-critical` | Threshold lines and bands |

Series colors come from `Palette` (`ChartPalettes`: default, Grafana, Material and single-hue shades, light and dark) or `Color`.  

## ✅ Supported features

| Category | Detail |
|---|---|
| **Charts** | Line, area and bar series mixed in one chart, areas stacked by `StackGroup`, KPI tiles, line, area and bar sparklines, basic and segmented bar gauges |
| **Axes** | A date and time axis with a sliding window, fixed ends and calendar ticks in a time zone, a numeric axis with fixed and soft ends, zero and nice ranges |
| **Data** | `Items` with typed `X` and `Y` delegates, `ChartRingBuffer<T>` read incrementally, missing values (`NaN`) as gaps |
| **Interaction** | A crosshair and a tooltip under the pointer (hover with a mouse, while touching on a touch screen), shared by `TrackingGroup`, and legend toggles |
| **Look** | Legend as a list or a table with last, minimum, maximum and mean, threshold lines and bands, a pulsing live point, CSS variables and palettes |
| **Size** | The width follows the container without scaling the text; before it is measured the chart is drawn 640 wide and scaled |

Not yet: category and numeric X axes, pies, radars, heatmaps, state timelines, scatter and range bars, markers, smooth lines, annotations, selection, downsampling and animations other than the live point.  
The tooltip formats values with `ValueFormat` (`N`, `F` and `P` formats and the compact number format) and does not call `ValueFormatter`.  

## 📖 API

### MusselChartView

| Name | Type | Default | Description |
|---|---|---|---|
| `Height` | `double` | `240` | Height in pixels; the width follows the container |
| `Palette` | `IReadOnlyList<string>?` | `ChartPalettes.DefaultLight` | Series colors |
| `LegendVisible` | `bool?` | `null` | `null` shows the legend for two or more series with a title |
| `LegendMode`, `LegendValues` | `ChartLegendMode`, `ChartLegendValues` | `List`, `None` | A list, or a table with `Last`, `Min`, `Max` and `Mean` of the points in view |
| `LegendToggleEnabled` | `bool` | `false` | Clicking a legend item hides or shows its series; the last visible series stays |
| `TrackballMode` | `ChartTrackballMode` | `Hover` | `None` turns the crosshair and the tooltip off |
| `TooltipVisible` | `bool` | `true` | `false` keeps only the crosshair and the points |
| `TrackingGroup`, `TrackingGroupMode` | `string?`, `ChartTrackingGroupMode` | `null`, `Crosshair` | Charts of the same group follow the chart under the pointer; `Tooltip` also shows their tooltips |
| `Thresholds`, `ThresholdsMode` | `IReadOnlyList<ChartThreshold>?`, `ChartThresholdsMode` | `null`, `None` | Threshold `Lines` and `Bands` on the value axis |
| `Culture`, `TimeZone` | `CultureInfo?`, `TimeZoneInfo?` | current culture, local zone | Formats and the zone of the times; a `DateTime` without a kind is read in that zone |
| `EmptyText` | `string?` | `null` | Text drawn when there is no data |

`DateTimeAxis` takes `WindowSize`, `Minimum`, `Maximum`, `LabelFormat` and `LabelFormatter`, and `NumericAxis` takes `Minimum`, `Maximum`, `SoftMinimum`, `SoftMaximum`, `IncludeZero`, `NiceRange`, `LabelFormat` and `LabelFormatter`.  
Both take `IsVisible`, `LabelsVisible`, `MaxLabelCount` and `GridLinesVisible`.  

### Series

| Series | Parameters |
|---|---|
| All | `Items`, `X`, `Y`, `Title`, `Color`, `IsVisible`, `LegendVisible`, `ValueFormat`, `ValueFormatter` |
| `LineSeries` | `Thickness` (2), `LiveIndicatorVisible` |
| `AreaSeries` | The line parameters, `FillOpacity` (0.1) and `StackGroup` |
| `BarSeries` | `BarWidthRatio` (0.6) and `MaxBarWidth` (24) |

### Widgets

| Component | Parameters |
|---|---|
| `MusselStatView` | `Title`, `Value`, `ValueFormat`, `ValueFormatter`, `Unit`, `TrendValues`, `TrendPlacement`, `Thresholds`, `ColorMode` (`None`, `Value`, `Indicator`, `Background`), `FillMode`, `Fill`, `Palette`, `EmptyText`, `Culture`; the tile fills the height of its container |
| `MusselSparklineView` | `Values`, `Kind` (`Line`, `Area`, `Bar`), `Color`, `Palette`, `Thickness`, `Minimum`, `Maximum`, `Height` |
| `MusselBarGaugeView` | `Items` (`ChartEntry` with a label, a value, a color, a maximum and a target), `Minimum`, `Maximum`, `DisplayMode` (`Basic`, `Segmented`), `SegmentWidth`, `BarThickness`, `LabelPosition`, `ValueVisible`, `ValueMode` (`Value`, `Percent`, `ValueOfMaximum`), `ValueFormat`, `ValueFormatter`, `TrackVisible`, `Thresholds`, `Color`, `Palette`, `Culture` |

Every component passes other attributes such as `class` and `id` to its root element.  
`Example` has a monitoring board, charts and widgets.  
