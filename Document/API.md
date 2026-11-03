# MusselChart API reference

## 📈 MusselChartView

Draws line, area and bar series on a date and time axis.  
Axes and series are written as child components, and every component passes other attributes such as `class` and `id` to its root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Height` | `double` | `240` | Height in pixels; the width follows the container without scaling the text |
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

### Axes

| Axis | Parameters |
|---|---|
| Both | `IsVisible`, `LabelsVisible`, `LabelFormat`, `MaxLabelCount`, `GridLinesVisible` |
| `DateTimeAxis` | `WindowSize`, `Minimum`, `Maximum`, `LabelFormatter` |
| `NumericAxis` | `Minimum`, `Maximum`, `SoftMinimum`, `SoftMaximum`, `IncludeZero`, `NiceRange`, `LabelFormatter` |

### Series

| Series | Parameters |
|---|---|
| All | `Items`, `X`, `Y`, `Title`, `Color`, `IsVisible`, `LegendVisible`, `ValueFormat`, `ValueFormatter` |
| `LineSeries` | `Thickness` (2), `LiveIndicatorVisible` |
| `AreaSeries` | The line parameters, `FillOpacity` (0.1) and `StackGroup` |
| `BarSeries` | `BarWidthRatio` (0.6) and `MaxBarWidth` (24) |

The tooltip formats values with `ValueFormat` (`N`, `F` and `P` formats and the compact number format) and does not call `ValueFormatter`.  

## 🔢 MusselStatView

A KPI tile that fills the height of its container.  

| Name | Description |
|---|---|
| `Title`, `Value`, `Unit` | Title, value and unit; `NaN` shows `EmptyText` without the unit |
| `ValueFormat`, `ValueFormatter`, `Culture` | Format of the value; the default is the compact number |
| `TrendValues`, `TrendPlacement` | Trend line at the `Bottom`, in the `Background` or `None` |
| `Thresholds` | State by value |
| `ColorMode`, `FillMode`, `Fill`, `Palette` | `None`, `Value`, `Indicator` or `Background`, solid or gradient |
| `LayoutMode` | `Vertical` stacks the title and the value; `Horizontal` puts the value and the unit at the top right |

## ✨ MusselSparklineView

| Name | Description |
|---|---|
| `Values`, `Kind` | Values as a `Line`, an `Area` or `Bar`s |
| `Color`, `Palette`, `Thickness` | Colors and the line width |
| `Minimum`, `Maximum`, `Height` | Fixed range and the height in pixels |

## 📶 MusselBarGaugeView

| Name | Description |
|---|---|
| `Items` | `ChartEntry` with a label, a value, a color, a maximum and a target |
| `Minimum`, `Maximum` | Range of every item; the maximum of an item comes first |
| `DisplayMode`, `SegmentWidth`, `BarThickness` | `Basic` or `Segmented` bars |
| `LabelPosition`, `ValueVisible`, `ValueMode` | Names at the `Top`, on the `Left` or `None`, and the value as a `Value`, a `Percent` or a `ValueOfMaximum` |
| `ValueFormat`, `ValueFormatter`, `Culture` | Format of the numbers |
| `TrackVisible`, `Thresholds`, `Color`, `Palette` | Track, threshold colors by percentage and the bar color |

## 🧰 ChartRingBuffer

| Member | Description |
|---|---|
| `ChartRingBuffer<T>(capacity)` | Keeps the latest `capacity` items; thread safe |
| `Push`, `Clear` | Adds an item, or removes every item so that charts read again |
| `Count`, `Capacity`, `Sequence`, `ToArray` | Items in the buffer, the size, the number of items pushed so far and a copy |

## 🎨 Theme

| Variable | Use |
|---|---|
| `--mussel-primary-text`, `--mussel-secondary-text`, `--mussel-muted-text` | Text, axis labels and empty text |
| `--mussel-grid-line`, `--mussel-track` | Grid lines, legend lines and the tracks of gauges |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | Tooltip |
| `--mussel-good`, `--mussel-warning`, `--mussel-serious`, `--mussel-critical` | Threshold lines and bands |

Link `_content/MusselChart/musselchart.css` and add `mussel-dark` to a container for the dark values.  

## 🚧 Not supported yet

Category and numeric X axes, pies, radars, heatmaps, state timelines, scatter and range bars, markers, smooth lines, annotations, selection, downsampling, animations other than the live point, and the `Auto` layout of KPI tiles.  
