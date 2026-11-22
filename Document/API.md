# MusselChart API reference

## 📈 MusselChartView

Draws line, area, bar, range bar, scatter and bubble series on a date and time, a numeric or a category axis, mixed in one chart.  
Axes and series are written as child components, and every component passes other attributes such as `class` and `id` to its root element. The child components are rendered again when the chart gets parameters, not when the chart redraws itself. The element a chart measures (the root of `MusselChartView`, and the plot of the pie, radial bar, radar, heatmap and state timeline views) gets `data-attached` once its script is attached, and loses it when the script is disposed.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Height` | `double` | `240` | Height in pixels; the width follows the container without scaling the text |
| `Palette` | `IReadOnlyList<string>?` | `ChartPalettes.DefaultLight` | Series colors |
| `Orientation` | `ChartOrientation` | `Vertical` | `Horizontal` swaps the axes for horizontal bars and rankings, with the first X at the top |
| `StackMode` | `ChartStackMode` | `Normal` | `Percent` scales each stack to 100% |
| `LegendVisible` | `bool?` | `null` | `null` shows the legend for two or more series with a title |
| `LegendMode`, `LegendValues` | `ChartLegendMode`, `ChartLegendValues` | `List`, `None` | A list, or a table with `Last`, `Min`, `Max` and `Mean` of the points in view |
| `LegendToggleEnabled` | `bool` | `false` | Clicking a legend item hides or shows its series; the last visible series stays |
| `TrackballMode` | `ChartTrackballMode` | `Hover` | `None` turns the crosshair and the tooltip off for the pointer over this chart; a chart in a `TrackingGroup` still follows the others |
| `TooltipVisible` | `bool` | `true` | `false` keeps only the crosshair and the points |
| `TrackingGroup`, `TrackingGroupMode` | `string?`, `ChartTrackingGroupMode` | `null`, `Crosshair` | Charts of the same group follow the chart under the pointer; `Tooltip` also shows their tooltips |
| `Thresholds`, `ThresholdsMode` | `IReadOnlyList<ChartThreshold>?`, `ChartThresholdsMode` | `null`, `None` | Threshold `Lines` and `Bands` on the value axis, in the `Color` of each threshold, the color of its `Level`, or `--mussel-secondary-text` without either |
| `Culture`, `TimeZone` | `CultureInfo?`, `TimeZoneInfo?` | current culture, local zone | Formats and the zone of the times; a `DateTime` without a kind is read in that zone; positions and lengths follow real time across a change of the offset, so the repeated hour of a change shows its labels twice; the default time labels use `/` and `:` in every culture, with the day or the month first as in the culture, and the tooltip adds the year when the view crosses a year |
| `EmptyText` | `string?` | `null` | Text drawn when there is no data |

A chart keeps the space of `Height` and stays hidden until the browser has measured its width, at most for a second, so that it opens at its final size. While an interactive chart is prerendered, it stays hidden until it starts.  
The pie, radial bar, radar, heatmap and state timeline views do the same.  

### Axes

| Axis | Parameters |
|---|---|
| All | `IsVisible`, `LabelsVisible`, `LabelFormat`, `MaxLabelCount`, `GridLinesVisible` |
| `DateTimeAxis` | The X axis of `X`: `WindowSize`, `Minimum`, `Maximum`, `LabelFormatter`; `WindowSize` is the time up to `Maximum` or the last point, without `Minimum`; `Minimum` and `Maximum` stay as set, so a bar or a bubble at them is cut in half, while an end taken from the points, the end of a window included, leaves half the spacing of bars and the radius of the largest bubble beyond them; a `Maximum` that is not after `Minimum` fixes neither end, and an end that the points are beyond is kept with the other end a second away, within the range of `DateTime` |
| `CategoryAxis` | The X axis of `Category`: `LabelPlacement` (`BetweenTicks` or `OnTicks`), `LabelMaxLength`, `LabelFormatter` |
| `NumericXAxis` | The X axis of `XValue`, with the parameters of `NumericAxis` |
| `NumericAxis` | The value axis: `Minimum`, `Maximum`, `SoftMinimum`, `SoftMaximum`, `IncludeZero`, `NiceRange`, `LabelFormatter`; a value that is not finite is not used, a `Minimum` that is not below `Maximum` fixes neither end, and an end that the values are beyond is kept with the other end 1 away, or a tenth of the end away when 1 is below its precision |

Without an X axis, the first series chooses it from the delegate it has, and the ends of a numeric X axis are not widened to the next tick.  
Without `LabelFormat` and `LabelFormatter`, the numbers of the value axis and of a numeric X axis take the compact number format with the decimals of the interval between ticks, per unit with `K`, `M`, `B` and `T`, so that next labels differ.  
Categories merge the names of all series in order of appearance, hidden series included, and overlapping labels are thinned keeping both ends.  

### Series

| Series | Parameters |
|---|---|
| All | `Items`, one of `X` (`DateTime`), `XValue` (`double`) or `Category` (`string`), `Y`, `Title`, `Color`, `IsVisible`, `LegendVisible`, `ValueFormat`, `ValueFormatter` |
| `LineSeries` | `Thickness` (2), `LiveIndicatorVisible` (a point on the last point, while it is inside the axes) |
| `AreaSeries` | The line parameters, `FillOpacity` (0.1) and `StackGroup` |
| `BarSeries` | `BarWidthRatio` (0.6), `MaxBarWidth` (24) and `StackGroup` |
| `RangeBarSeries` | `Low` and `High` instead of `Y`, `BarWidthRatio` (0.6) and `MaxBarWidth` (24) |
| `ScatterSeries` | `MarkerSize` (8), `MarkerShape` (`Circle`, `Square` or `Diamond`) and `FillOpacity` (0.5) |
| `BubbleSeries` | The scatter parameters, `Size`, `MinBubbleSize` (8), `MaxBubbleSize` (40) and `SizeIncludesZero` (`true`) |

The tooltip formats values with `ValueFormat` (`N`, `F` and `P` formats and the compact number format) and does not call `ValueFormatter`. Without `ValueFormat`, a series takes the `LabelFormat` of the value axis in the tooltip. Without `ValueFormat` and `ValueFormatter`, it takes the `LabelFormat` or `LabelFormatter` of the value axis in the legend. Neither is taken on an axis of only 100% stacks. A point outside the axes gets no dot on hover and keeps its row in the tooltip.  

| Item | Rule |
|---|---|
| Grouping | Each bar series and each stack takes an equal slot of the column, narrowed by `MaxBarWidth` and centered; a group uses the `BarWidthRatio` of its first visible series |
| Stacking | Series of the same kind and the same `StackGroup` stack at the same X: positive values up from 0, negative values down from 0, and `Percent` scales each side to its own total. Segments of bars are separated by 2 pixels. The tooltip keeps the original values, and an axis of only 100% stacks labels percentages |
| Range bars | A bar from the low to the high value; swapped values are drawn from the smaller one, a point without both values is skipped, the value axis does not include 0 and the tooltip shows `12 – 25` |
| Scatter | Points filled at `FillOpacity` and outlined with the series color; a point past an end of the X axis is drawn while its shape reaches into the plot, and the crosshair and the tooltip leave it out. With only scatter and bubble series, the crosshair snaps to the nearest point inside the plot in both directions and the tooltip shows only that point |
| Bubbles | The area grows from 0 to the largest size among the visible bubble series: 0 and below get `MinBubbleSize` and the largest value gets `MaxBubbleSize`. `SizeIncludesZero` set to `false` starts from the smallest value. A point without a size is skipped, larger bubbles are drawn first, and the automatic ends of the axes grow by the radius of the largest bubble, as does the end of a time window without `Maximum`, which keeps its length |
| Updates | Only a time axis with absolute values appends new points, and only while every series reads a `ChartRingBuffer` and the series of each stack read the same one; with a series that reads an array, on numeric and category axes and in `Percent` stacks, every point is read again at each render. A new `TimeZone` or a new delegate such as `Y` reads every point again; a static lambda, or one that uses only `this`, stays the same at each render, while one that captures a local variable does not. Scatter and bubble points already drawn keep their attributes when the window moves, and are written again when the scale of an axis or of the bubbles changes, as with a growing automatic time range or a new value range |

## 🍩 MusselPieChartView

Shows the shares of a whole from `ChartEntry` items.  
The default is a donut with the total in the center; `InnerRadius = 0` draws a pie and `StartAngle = 270` with `SweepAngle = 180` the upper half.  
Angles are degrees clockwise from 12 o'clock.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<ChartEntry>?` | `null` | Label, value and color of each slice; values that are not positive or not finite are skipped |
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the colors in data order; slices beyond the palette are gray |
| `InnerRadius` | `double` | `0.6` | Hole as a fraction of the radius, from 0 to 0.95 |
| `StartAngle`, `SweepAngle` | `double` | `0`, `360` | Start and length of the sweep |
| `SegmentGap` | `double` | `2` | Parallel gap between slices, none at the ends of a partial sweep |
| `SortMode` | `ChartSortMode` | `None` | Order of the slices; colors keep the data order |
| `MaxSlices`, `OthersThreshold`, `OthersLabel` | `int`, `double`, `string` | `8`, `0`, `Other` | The smallest items over the limit and two or more items below the share become one merged slice |
| `LabelsMode`, `LabelContent` | `ChartPieLabelsMode`, `ChartPieLabelContent` | `None`, `Percent` | `Inside` draws the labels that fit in their slices; `Outside` draws them beside the pie with leader lines |
| `LegendVisible`, `LegendPosition`, `LegendValueMode` | `bool?`, `ChartLegendPosition`, `ChartLegendValueMode` | `null`, `Bottom`, `None` | The legend and the value or the percentage in it; `null` shows the legend |
| `LegendToggleEnabled` | `bool` | `false` | Clicking a legend item hides or shows the slice by its name; the merged slice and the last visible slice stay |
| `CenterValueMode`, `CenterValue`, `CenterLabel` | `ChartCenterValueMode`, `double?`, `string?` | `Auto`, `null`, `null` | The total or `CenterValue` in the hole, shrunk to fit, with the label under it; a value that is not finite shows neither |
| `CenterValueFormat`, `CenterValueFormatter` | `string?`, `Func<double, string>?` | `null` | Format of the center value; when neither is set, `ValueFormat` and `ValueFormatter` apply |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the slice values; the default is the compact number |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering dims the other slices and shows the tooltip |

## ⭕ MusselRadialBarView

Concentric rings that compare several progress values, the first item being the outermost ring, like the Grafana gauge.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<ChartEntry>?` | `null` | Label, value, color and the maximum of each ring |
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the ring colors |
| `Minimum`, `Maximum` | `double` | `0`, `100` | Range of every ring; the maximum of an item comes first; a range that is not finite, or whose maximum is not above `Minimum`, makes the item count as a value that is not finite, for `Total` and `EmptyText` too |
| `StartAngle`, `SweepAngle` | `double` | `0`, `360` | Start and length of the rings; `250` and `220` give the gauge open at the bottom |
| `InnerRadius`, `RingThickness`, `RingSpacing` | `double`, `double?`, `double` | `0.4`, `null`, `4` | `null` divides the area outside `InnerRadius`, up to 1/6 of the radius |
| `CapStyle` | `ChartCapStyle` | `Flat` | Ends of the rings and the track |
| `DisplayMode`, `SegmentCount`, `SegmentGap` | `ChartRadialBarMode`, `int`, `double` | `Basic`, `20`, `2` | `Segmented` lights a cell when its start is before the value; fewer cells on short rings; `OverflowMode` and the `FillMode` and `CapStyle` of the rings work in `Basic` only, while the threshold ring follows `FillMode` in both |
| `FillMode` | `ChartFillMode` | `Solid` | `Gradient` draws a sweep of the threshold colors, or of the shades of the ring color |
| `TrackVisible`, `BarGlow`, `ThresholdMarkersVisible` | `bool` | `true`, `false`, `false` | The rest of each ring; a glow under the values and the threshold ring; a thin ring of the threshold ranges outside the rings |
| `OverflowMode` | `ChartOverflowMode` | `Clamp` | `Wrap` draws the part over the maximum on top of the first lap in a darker color |
| `Thresholds` | `IReadOnlyList<ChartThreshold>?` | `null` | Colors by the percentage of the range from `Minimum` to `Maximum` |
| `CenterValueMode`, `CenterText`, `CenterLabel` | `ChartCenterValueMode`, `string?`, `string?` | `Auto`, `null`, `null` | `Auto` shows the outermost ring as a percentage with its name, or nothing when its value is not finite, and `Total` the sum of the finite values, or nothing when the sum is not finite; `CenterText` takes the place of the value in every mode, `None` included, while an item has a finite value; with no finite value in any item, `EmptyText` is drawn instead of the center |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the values; the default is the compact number |
| `LegendVisible`, `LegendPosition`, `LegendValueMode` | `bool?`, `ChartLegendPosition`, `ChartLegendValueMode` | `null`, `Right`, `None` | The legend and the value or the percentage in it; `null` shows the legend |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering dims the other rings and shows the tooltip |

⚠️ `Gradient` uses a CSS `conic-gradient` in a `foreignObject`, since SVG has no sweep gradient.  

## 🕸 MusselRadarChartView

Compares several series on axes that spread from the center, one axis per item.  
`RadarSeries` are written as child components, rendered again when the chart gets parameters.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Height`, `Palette` | `double`, `IReadOnlyList<string>?` | `240`, `ChartPalettes.DefaultLight` | Height in pixels and the series colors |
| `Minimum`, `Maximum` | `double?` | `null` | Value at the center and at the outer ring; `null`, or a value that is not finite, is chosen from the visible series with 0 included and nice levels, and so are both ends when `Minimum` is not below `Maximum` |
| `GridShape`, `LevelCount` | `ChartRadarGridShape`, `int` | `Polygon`, `5` | Shape of the rings and the most rings; fewer than 3 items always use circles |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the levels and of the series that set neither `ValueFormat` nor `ValueFormatter`; without either, the levels keep the decimals of the interval between them |
| `LegendVisible`, `LegendPosition`, `LegendToggleEnabled` | `bool?`, `ChartLegendPosition`, `bool` | `null`, `Bottom`, `false` | `null` shows the legend for two or more series with a title; clicking an item hides or shows its series, and the last visible series stays |
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
| `Minimum`, `Maximum` | `double?` | `null` | Values at the two ends of the colors; `null`, or a value that is not finite, takes the smallest and the largest value, and so do both ends when `Minimum` is not below `Maximum`; an end that the values are beyond is kept with the other end 1 away, or a tenth of the end away when 1 is below its precision |
| `ColorScale` | `IReadOnlyList<string>` | `ChartPalettes.OrangesLight` | Hex colors from low to high values, two or more, with their alpha; `OrangesDark` suits a dark theme |
| `CellGap` | `double` | `1` | Space after each cell |
| `ValueFormat`, `ValueFormatter`, `Culture` | | | Format of the values; without either format, the labels of the color scale keep the decimals of the interval between them; the week starts on the `FirstDayOfWeek` of the culture |
| `LegendVisible`, `LegendPosition` | `bool?`, `ChartLegendPosition` | `null`, `Bottom` | The color scale and its side; `null` shows it |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | Hovering frames the cell and shows the tooltip |

Rows and columns follow the order of appearance, the first value of a cell is used and a cell without a value is drawn in `--mussel-track`. A matrix has a cell for every pair of a row name and a column name, those without a value included; when the cells are more than an `int` holds, it draws no cells and shows `EmptyText`.  
A calendar runs from the first to the last date, up to the latest 1,000 weeks, with square cells and the month above the week where it starts. The months and the dates of the tooltips use the Gregorian calendar of the culture.  

## 🚥 MusselStateTimelineView

Shows how states change over time, one row per entity, like the Grafana state timeline.  
Each item is the start of a state, which lasts until the next start in its row.  

| Name | Type | Default | Description |
|---|---|---|---|
| `Items` | `IReadOnlyList<TItem>?` | `null` | Items |
| `Row`, `Start`, `End` | `Func<TItem, string?>?`, `Func<TItem, DateTime>?`, `Func<TItem, DateTime?>?` | `null` | Row name, the start and the optional end; without `Row` everything is one row; an end that is not after the start is ignored, and the state lasts until the next start of its row |
| `State` | `Func<TItem, object?>?` | `null` | Text or a number; `null`, `NaN` and infinities end the previous state |
| `Height` | `double` | `240` | Height in pixels |
| `Minimum`, `Maximum` | `DateTime?` | `null` | Time range; `null` takes the first start and the last start or end, so pass the current time as `Maximum`; a `Maximum` that is not after `Minimum` fixes neither end, and an end that the states are beyond is kept with the other end a second away |
| `ValueMappings` | `IReadOnlyList<ChartValueMapping>?` | `null` | `Value` to match, `Text`, `Color` and `Level` of a state |
| `Thresholds` | `IReadOnlyList<ChartThreshold>?` | `null` | Steps of numeric states, named by `Label` or like `< 60` and `60+`. When some states are numbers, the legend shows the steps |
| `MergeValues` | `bool` | `true` | Merges equal states next to each other, and numbers in the same threshold step, which then do not use `ValueMappings` |
| `ShowValue`, `AlignValue` | `ChartVisibilityMode`, `ChartValueAlignment` | `Auto`, `Left` | Text on each band; `Auto` writes it where its first two characters fit |
| `RowHeight`, `FillOpacity`, `LineWidth` | `double` | `0.9`, `0.7`, `0` | Share of the height taken by the bands, the fill and the outline |
| `ValueFormat`, `ValueFormatter`, `Culture`, `TimeZone` | | | Format of numeric states and the zone of the times; as in the main chart, the times use `/` and `:`, the order of the day and the month follows the culture, and the tooltip adds the year when the view crosses a year |
| `LegendVisible`, `LegendPosition` | `bool?`, `ChartLegendPosition` | `null`, `Bottom` | The states or the threshold steps; `null` shows them |
| `TrackballMode`, `TooltipVisible`, `EmptyText` | | `Hover`, `true`, `null` | The tooltip shows the start and the end, the state and the duration (`2h 30m`) |

Numbers without thresholds take a green to yellow to red scale between the smallest and the largest number of their row and have no legend, and text without a mapping is green, or with thresholds in the color of the lowest step. Thresholds without a step at `-∞` get one below their first step, named like `< 60`, in the color of a step without a level. A lone step at `-∞` without `Label` has no name: its bands carry no text, the legend leaves it out, and the tooltip leaves the state empty.  

## 🔢 MusselStatView

A KPI tile that fills the height of its container.  

| Name | Description |
|---|---|
| `Title`, `Value`, `Unit` | Title, value and unit; `NaN` and infinities show `EmptyText` (`-` when it is not set) without the unit |
| `ValueFormat`, `ValueFormatter`, `Culture` | Format of the value; the default is the compact number |
| `TrendValues`, `TrendPlacement` | Trend line at the `Bottom`, in the `Background` or `None`; values that are not finite are skipped and the line joins the others |
| `Thresholds` | State by value |
| `ColorMode`, `FillMode`, `Fill`, `Palette` | `None`, `Value`, `Indicator` or `Background`, solid or gradient |
| `LayoutMode` | `Vertical` stacks the title and the value; `Horizontal` puts the value and the unit at the top right |

## ✨ MusselSparklineView

| Name | Description |
|---|---|
| `Values`, `Kind` | Values as a `Line`, an `Area`, `Bar`s or `WinLoss`, where wins fill the upper half and losses the lower half; a value that is not finite is skipped, so a line or an area joins the others and a bar leaves its place empty |
| `Color`, `Palette`, `Thickness` | Colors and the line width |
| `NegativeColor` | Negative bars and losses; `null` is the color of the bars and `--mussel-critical` for losses |
| `Minimum`, `Maximum`, `Height` | Fixed range and the height in pixels; a value that is not finite is not fixed, and a `Minimum` that is not below `Maximum` fixes neither end; `Bar` widens the range to include 0, and `WinLoss` does not use it |

## 📶 MusselBarGaugeView

| Name | Description |
|---|---|
| `Items` | `ChartEntry` with a label, a value, a color, a maximum and a target; a value that is not finite shows `-` and an empty bar |
| `Minimum`, `Maximum` | Range of every item; the maximum of an item comes first; a range that is not finite, or whose maximum is not above `Minimum`, shows `-` and an empty bar |
| `DisplayMode`, `SegmentWidth`, `BarThickness` | `Basic` or `Segmented` bars |
| `LabelPosition`, `ValueVisible`, `ValueMode` | Names at the `Top`, on the `Left` (one column for every bar, up to 40 % of the width) or `None`, and the value as a `Value`, a `Percent` or a `ValueOfMaximum` |
| `ValueFormat`, `ValueFormatter`, `Culture` | Format of the numbers |
| `TrackVisible`, `Thresholds`, `Color`, `Palette` | Track, threshold colors by percentage and the bar color; `Segmented` always draws the cells that are not lit |

## 🧰 ChartRingBuffer

| Member | Description |
|---|---|
| `ChartRingBuffer<T>(capacity)` | Keeps the latest `capacity` items; thread safe |
| `Push`, `Clear` | Adds an item, or removes every item so that charts read again |
| `Count`, `Capacity`, `Sequence`, `ToArray` | Items in the buffer, the size, the number of items pushed so far and a copy |
| `CollectionChanged` | Raised as `Reset` after each `Push` and `Clear`, on the thread that called them |

Every view watches items that implement `INotifyCollectionChanged`, such as this buffer and `ObservableCollection`, and draws again by itself when they change.  
Series of one chart that share a buffer read the same items, and items pushed while the chart reads show at the next update.  

## 🎨 Theme

Link `_content/MusselChart/MusselChart.css` in the page head. Colors come from CSS variables, which an app changes from any selector and in any file order. `mussel-dark` on a container or on the view itself switches to the dark values, the same class as the other Mussel libraries. A value an app sets on the element with `mussel-dark`, or on an element inside it, is used in both themes, so include `.mussel-dark` in the selector to change a dark value. A shared color set above that element, such as on `:root`, gives way to the dark values.  

These are shared with the other Mussel libraries. Their light defaults follow the look of each library, so set them on a container to give several libraries the same colors.  

| Variable | Use |
|---|---|
| `--mussel-primary-text`, `--mussel-secondary-text`, `--mussel-muted-text` | Text, axis labels and empty text |
| `--mussel-grid-line`, `--mussel-track` | Grid lines, legend lines, the tracks of gauges and rings, and empty cells of heatmaps |
| `--mussel-unlit` | Cells of segmented rings that are not lit |
| `--mussel-tooltip-background`, `--mussel-tooltip-border`, `--mussel-tooltip-text` | Tooltip |
| `--mussel-good`, `--mussel-warning`, `--mussel-serious`, `--mussel-critical` | Threshold lines and bands, and the losses of sparklines |
| `--mussel-font-family` | Font family |

The level colors of `MusselRadialBarView`, `MusselBarGaugeView`, `MusselStatView` and `MusselStateTimelineView` stay the dark values in both themes and do not follow these variables. Series, slices and rings beyond the palette, and every view with an empty palette, are gray (`#B5B3AA`). Shades and gradients of a color, and the text on it, are worked out from hex colors (`#RGB`, `#RGBA`, `#RRGGBB` or `#RRGGBBAA`); shades keep the alpha of the color. The text drawn on a color, that is the inside labels of `MusselPieChartView`, the text on the bands of `MusselStateTimelineView` and the text of a `Gradient` tile of `MusselStatView`, is white or dark by the lightness of the color, which does not count its transparency, so for translucent colors set `fill` on `.mussel-pie-label` or `.mussel-timeline-text` in a global stylesheet. The text of a `Gradient` tile cannot be changed from an app, so give such a tile an opaque `Fill`. The colors of `MusselStatView` and `MusselRadialBarView`, and the marks of tooltips, are CSS colors of up to 64 characters; a color that is longer, with `url(`, or with characters other than letters, digits, space and `# ( ) , . % - /` is not used: the tile and the rings take the color they would take without it, down to gray, and a tooltip shows no mark.  

## 🚧 Not supported yet

A second value axis, data labels, markers and smooth lines of series, annotations, selection, downsampling, vertical bar gauges, animations other than the live point, and the `Auto` layout of KPI tiles.  
