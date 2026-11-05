# MusselChart API reference

## 📈 MusselChartView

Draws line, area, bar, range bar, scatter and bubble series on a date and time, a numeric or a category axis, mixed in one chart.  
Axes and series are written as child components, and every component passes other attributes such as `class` and `id` to its root element.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Height` | `double` | `240` | Height in pixels; the width follows the container without scaling the text |
| `Palette` | `IReadOnlyList<string>?` | `ChartPalettes.DefaultLight` | Series colors |
| `Orientation` | `ChartOrientation` | `Vertical` | `Horizontal` swaps the axes for horizontal bars and rankings, with the first X at the top |
| `StackMode` | `ChartStackMode` | `Normal` | `Percent` scales each stack to 100% |
| `LegendVisible` | `bool?` | `null` | `null` shows the legend for two or more series with a title |
| `LegendMode`, `LegendValues` | `ChartLegendMode`, `ChartLegendValues` | `List`, `None` | A list, or a table with `Last`, `Min`, `Max` and `Mean` of the points in view |
| `LegendToggleEnabled` | `bool` | `false` | Clicking a legend item hides or shows its series; the last visible series stays |
| `TrackballMode` | `ChartTrackballMode` | `Hover` | `None` turns the crosshair and the tooltip off |
| `TooltipVisible` | `bool` | `true` | `false` keeps only the crosshair and the points |
| `TrackingGroup`, `TrackingGroupMode` | `string?`, `ChartTrackingGroupMode` | `null`, `Crosshair` | Charts of the same group follow the chart under the pointer; `Tooltip` also shows their tooltips |
| `Thresholds`, `ThresholdsMode` | `IReadOnlyList<ChartThreshold>?`, `ChartThresholdsMode` | `null`, `None` | Threshold `Lines` and `Bands` on the value axis |
| `Culture`, `TimeZone` | `CultureInfo?`, `TimeZoneInfo?` | current culture, local zone | Formats and the zone of the times; a `DateTime` without a kind is read in that zone |
| `EmptyText` | `string?` | `null` | Text drawn when there is no data |

A chart keeps the space of `Height` and stays hidden until the browser has measured its width, at most for a second, so that it opens at its final size.  
The pie, radial bar, radar, heatmap and state timeline views do the same.  

### Axes

| Axis | Parameters |
|---|---|
| All | `IsVisible`, `LabelsVisible`, `LabelFormat`, `MaxLabelCount`, `GridLinesVisible` |
| `DateTimeAxis` | The X axis of `X`: `WindowSize`, `Minimum`, `Maximum`, `LabelFormatter` |
| `CategoryAxis` | The X axis of `Category`: `LabelPlacement` (`BetweenTicks` or `OnTicks`), `LabelMaxLength`, `LabelFormatter` |
| `NumericXAxis` | The X axis of `XValue`, with the parameters of `NumericAxis` |
| `NumericAxis` | The value axis: `Minimum`, `Maximum`, `SoftMinimum`, `SoftMaximum`, `IncludeZero`, `NiceRange`, `LabelFormatter` |

Without an X axis, the first series chooses it from the delegate it has, and the ends of a numeric X axis are not widened to the next tick.  
Categories merge the names of all series in order of appearance, hidden series included, and overlapping labels are thinned keeping both ends.  

### Series

| Series | Parameters |
|---|---|
| All | `Items`, one of `X` (`DateTime`), `XValue` (`double`) or `Category` (`string`), `Y`, `Title`, `Color`, `IsVisible`, `LegendVisible`, `ValueFormat`, `ValueFormatter` |
| `LineSeries` | `Thickness` (2), `LiveIndicatorVisible` |
| `AreaSeries` | The line parameters, `FillOpacity` (0.1) and `StackGroup` |
| `BarSeries` | `BarWidthRatio` (0.6), `MaxBarWidth` (24) and `StackGroup` |
| `RangeBarSeries` | `Low` and `High` instead of `Y`, `BarWidthRatio` (0.6) and `MaxBarWidth` (24) |
| `ScatterSeries` | `MarkerSize` (8), `MarkerShape` (`Circle`, `Square` or `Diamond`) and `FillOpacity` (0.5) |
| `BubbleSeries` | The scatter parameters, `Size`, `MinBubbleSize` (8), `MaxBubbleSize` (40) and `SizeIncludesZero` (`true`) |

The tooltip formats values with `ValueFormat` (`N`, `F` and `P` formats and the compact number format) and does not call `ValueFormatter`.  

| Item | Rule |
|---|---|
| Grouping | Each bar series and each stack takes an equal slot of the column, narrowed by `MaxBarWidth` and centered; a group uses the `BarWidthRatio` of its first visible series |
| Stacking | Series of the same kind and the same `StackGroup` stack at the same X: positive values up from 0, negative values down from 0, and `Percent` scales each side to its own total. Segments of bars are separated by 2 pixels. The tooltip keeps the original values, and an axis of only 100% stacks labels percentages |
| Range bars | A bar from the low to the high value; swapped values are drawn from the smaller one, a point without both values is skipped, the value axis does not include 0 and the tooltip shows `12 – 25` |
| Scatter | Points filled at `FillOpacity` and outlined with the series color. With only scatter and bubble series, the crosshair snaps to the nearest point in both directions and the tooltip shows only that point |
| Bubbles | The area grows from 0 to the largest size among all the bubble series: 0 and below get `MinBubbleSize` and the largest value gets `MaxBubbleSize`. `SizeIncludesZero` set to `false` starts from the smallest value. A point without a size is skipped, larger bubbles are drawn first and the automatic ranges grow by the radius of the largest bubble |
| Updates | Only a time axis with absolute values appends new points; numeric and category axes and `Percent` stacks are read again at each render |

## 🍩 MusselPieChartView

Shows the shares of a whole from `ChartEntry` items.  
The default is a donut with the total in the center; `InnerRadius = 0` draws a pie and `StartAngle = 270` with `SweepAngle = 180` the upper half.  
Angles are degrees clockwise from 12 o'clock.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<ChartEntry>?` | `null` | Label, value and color of each slice; values that are not positive are skipped |
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the colors in data order; slices beyond the palette are gray |
| `InnerRadius` | `double` | `0.6` | Hole as a fraction of the radius, from 0 to 0.95 |
| `StartAngle`, `SweepAngle` | `double` | `0`, `360` | Start and length of the sweep |
| `SegmentGap` | `double` | `2` | Parallel gap between slices, none at the ends of a partial sweep |
| `SortMode` | `ChartSortMode` | `None` | Order of the slices; colors keep the data order |
| `MaxSlices`, `OthersThreshold`, `OthersLabel` | `int`, `double`, `string` | `8`, `0`, `Other` | The smallest items over the limit and two or more items below the share become one merged slice |
| `LabelsMode`, `LabelContent` | `ChartPieLabelsMode`, `ChartPieLabelContent` | `None`, `Percent` | `Inside` draws the labels that fit in their slices; `Outside` draws them beside the pie with leader lines |
| `LegendVisible`, `LegendPosition`, `LegendValueMode` | `bool?`, `ChartLegendPosition`, `ChartLegendValueMode` | `null`, `Bottom`, `None` | The legend and the value or the percentage in it |
| `LegendToggleEnabled` | `bool` | `false` | Clicking a legend item hides or shows the slice by its name; the merged slice and the last visible slice stay |
| `CenterValueMode`, `CenterValue`, `CenterLabel` | `ChartCenterValueMode`, `double?`, `string?` | `Auto`, `null`, `null` | The total or `CenterValue` in the hole, shrunk to fit, with the label under it |
| `CenterValueFormat`, `CenterValueFormatter` | `string?`, `Func<double, string>?` | `null` | Format of the center value; unset uses the value formats |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the slice values; the default is the compact number |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering dims the other slices and shows the tooltip |

## ⭕ MusselRadialBarView

Concentric rings that compare several progress values, the first item being the outermost ring, like the Grafana gauge.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<ChartEntry>?` | `null` | Label, value, color and the maximum of each ring |
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the ring colors |
| `Minimum`, `Maximum` | `double` | `0`, `100` | Range of every ring; the maximum of an item comes first |
| `StartAngle`, `SweepAngle` | `double` | `0`, `360` | Start and length of the rings; `250` and `220` give the gauge open at the bottom |
| `InnerRadius`, `RingThickness`, `RingSpacing` | `double`, `double?`, `double` | `0.4`, `null`, `4` | `null` divides the area outside `InnerRadius`, up to 1/6 of the radius |
| `CapStyle` | `ChartCapStyle` | `Flat` | Ends of the rings and the track |
| `DisplayMode`, `SegmentCount`, `SegmentGap` | `ChartRadialBarMode`, `int`, `double` | `Basic`, `20`, `2` | `Segmented` lights a cell when its start is before the value; fewer cells on short rings |
| `FillMode` | `ChartFillMode` | `Solid` | `Gradient` draws a sweep of the threshold colors, or of the shades of the ring color |
| `TrackVisible`, `BarGlow`, `ThresholdMarkersVisible` | `bool` | `true`, `false`, `false` | The rest of each ring, a glow under the values and a thin ring of the threshold ranges outside the rings |
| `OverflowMode` | `ChartOverflowMode` | `Clamp` | `Wrap` draws the part over the maximum on top of the first lap in a darker color |
| `Thresholds` | `IReadOnlyList<ChartThreshold>?` | `null` | Colors by the percentage of the maximum |
| `CenterValueMode`, `CenterText`, `CenterLabel` | `ChartCenterValueMode`, `string?`, `string?` | `Auto`, `null`, `null` | `Auto` shows the outermost ring as a percentage with its name, and `Total` the sum of the values |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the values; the default is the compact number |
| `LegendVisible`, `LegendPosition`, `LegendValueMode` | `bool?`, `ChartLegendPosition`, `ChartLegendValueMode` | `null`, `Right`, `None` | The legend and the value or the percentage in it |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering dims the other rings and shows the tooltip |

⚠️ `Gradient` uses a CSS `conic-gradient` in a `foreignObject`, since SVG has no sweep gradient.  

## 🕸 MusselRadarChartView

Compares several series on axes that spread from the center, one axis per item.  
`RadarSeries` are written as child components.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the series colors |
| `Minimum`, `Maximum` | `double?` | `null` | Value at the center and at the outer ring; `null` is chosen from the visible series with 0 included and nice levels |
| `GridShape`, `LevelCount` | `ChartRadarGridShape`, `int` | `Polygon`, `5` | Shape of the rings and the most rings; fewer than 3 items always use circles |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the levels and of the series without their own format |
| `LegendVisible`, `LegendPosition`, `LegendToggleEnabled` | `bool?`, `ChartLegendPosition`, `bool` | `null`, `Bottom`, `false` | `null` shows the legend for two or more series with a title; clicking an item hides or shows its series |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering marks the axis in that direction and shows the values of every series |

| `RadarSeries<TItem>` | Description |
|---|---|
| `Items`, `Category`, `Y` | Items, the name and the value; `ChartEntry` items need no delegates |
| `Title`, `Color`, `IsVisible`, `LegendVisible`, `ValueFormat`, `ValueFormatter` | As in the other series |
| `Thickness`, `FillOpacity` | Line width (2) and the opacity of the fill (0.15) |

Item names become axes in the order they first appear across all the series, hidden ones included.  
Items without a name take axes by position, an item without a value is skipped and its neighbors are joined, and a name repeated in one series keeps its first value.  

## 🟩 MusselHeatmapView

Shows values as shades of color, as a matrix such as days by hours, or as a calendar of daily values with one column per week.  
The colors follow the Grafana heatmap, spread over 64 steps.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<TItem>?` | `null` | Items; `ChartEntry` items make one row without delegates |
| `Row`, `Column`, `Value` | `Func<TItem, string?>?`, `Func<TItem, string?>?`, `Func<TItem, double>?` | `null` | Row name, column name and value of a matrix; without `Row` the matrix has one row |
| `Date` | `Func<TItem, DateTime>?` | `null` | Date of a calendar |
| `LayoutMode` | `ChartHeatmapLayoutMode` | `Matrix` | A `Matrix` or a `Calendar` |
| `Height` | `double` | `240` | Height in pixels |
| `Minimum`, `Maximum` | `double?` | `null` | Values at the two ends of the colors; `null` takes the smallest and the largest value |
| `ColorScale` | `IReadOnlyList<string>` | `ChartPalettes.OrangesLight` | Hex colors from low to high values, two or more; `OrangesDark` suits a dark theme |
| `CellGap` | `double` | `1` | Space after each cell |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the values; the week starts on the `FirstDayOfWeek` of the culture |
| `LegendVisible`, `LegendPosition` | `bool?`, `ChartLegendPosition` | `null`, `Bottom` | The color scale and its side |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering frames the cell and shows the tooltip |

Rows and columns follow the order of appearance, the first value of a cell is used and a cell without a value is drawn in `--mussel-track`.  
A calendar runs from the first to the last date, up to the latest 1,000 weeks, with square cells and the month above the week where it starts.  

## 🚥 MusselStateTimelineView

Shows how states change over time, one row per entity, like the Grafana state timeline.  
Each item is the start of a state, which lasts until the next start in its row.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<TItem>?` | `null` | Items |
| `Row`, `Start`, `End` | `Func<TItem, string?>?`, `Func<TItem, DateTime>?`, `Func<TItem, DateTime?>?` | `null` | Row name, the start and the optional end; without `Row` everything is one row |
| `State` | `Func<TItem, object?>?` | `null` | Text or a number; `null` ends the previous state |
| `Height` | `double` | `240` | Height in pixels |
| `Minimum`, `Maximum` | `DateTime?` | `null` | Time range; `null` takes the first start and the last start or end, so pass the current time as `Maximum` |
| `ValueMappings` | `IReadOnlyList<ChartValueMapping>?` | `null` | `Value` to match, `Text`, `Color` and `Level` of a state |
| `Thresholds` | `IReadOnlyList<ChartThreshold>?` | `null` | Steps of numeric states, named by `Label` or like `< 60` and `60+` |
| `MergeValues` | `bool` | `true` | Merges equal states next to each other, and numbers in the same threshold step |
| `ShowValue`, `AlignValue` | `ChartVisibilityMode`, `ChartValueAlignment` | `Auto`, `Left` | Text on each band; `Auto` writes it where its first two characters fit |
| `RowHeight`, `FillOpacity`, `LineWidth` | `double` | `0.9`, `0.7`, `0` | Share of the height taken by the bands, the fill and the outline |
| `ValueFormat`, `ValueFormatter`, `Culture`, `TimeZone` | | | Format of numeric states and the zone of the times |
| `LegendVisible`, `LegendPosition` | `bool?`, `ChartLegendPosition` | `null`, `Bottom` | The states or the threshold steps |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | The tooltip shows the start and the end, the state and the duration (`2h 30m`) |

Numbers without thresholds take a green to yellow to red scale between the smallest and the largest number of their row and have no legend, and text without a mapping is green.  

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
| `Values`, `Kind` | Values as a `Line`, an `Area`, `Bar`s or `WinLoss`, where wins fill the upper half and losses the lower half |
| `Color`, `Palette`, `Thickness` | Colors and the line width |
| `NegativeColor` | Negative bars and losses; `null` is the color of the bars and `--mussel-critical` for losses |
| `Minimum`, `Maximum`, `Height` | Fixed range and the height in pixels |

## 📶 MusselBarGaugeView

| Name | Description |
|---|---|
| `Items` | `ChartEntry` with a label, a value, a color, a maximum and a target |
| `Minimum`, `Maximum` | Range of every item; the maximum of an item comes first |
| `DisplayMode`, `SegmentWidth`, `BarThickness` | `Basic` or `Segmented` bars |
| `LabelPosition`, `ValueVisible`, `ValueMode` | Names at the `Top`, on the `Left` (one column for every bar, up to 40 % of the width) or `None`, and the value as a `Value`, a `Percent` or a `ValueOfMaximum` |
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
| `--mussel-grid-line`, `--mussel-track` | Grid lines, legend lines, the tracks of gauges and rings, and empty cells of heatmaps |
| `--mussel-unlit` | Cells of segmented rings that are not lit |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | Tooltip |
| `--mussel-good`, `--mussel-warning`, `--mussel-serious`, `--mussel-critical` | Threshold lines and bands, and the losses of sparklines |

Link `_content/MusselChart/MusselChart.css` and add `mussel-dark` to a container for the dark values.  

## 🚧 Not supported yet

A second value axis, data labels, markers and smooth lines of series, annotations, selection, downsampling, vertical bar gauges, animations other than the live point, and the `Auto` layout of KPI tiles.  
