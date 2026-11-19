const SVG_NS = 'http://www.w3.org/2000/svg';
const SUFFIXES = ['K', 'M', 'B', 'T'];
const numberFormats = new Map();

export function attach(element, dotnet) {
    const state = { width: 0, frame: 0, target: null, last: null };
    state.observer = new ResizeObserver(entries => {
        const width = Math.floor(entries[0].contentRect.width);
        if (width <= 0 || width === state.width) {
            return;
        }

        state.width = width;
        cancelAnimationFrame(state.frame);
        state.frame = requestAnimationFrame(() => dotnet.invokeMethodAsync('OnResize', width).catch(() => { }));
    });
    state.observer.observe(element);

    state.track = e => {
        if (element.dataset.trackball !== 'hover') {
            state.leave();
            return;
        }

        if (element.dataset.hover === 'item') {
            trackItem(element, state, e);
        } else {
            track(element, e);
        }
    };
    state.mutations = new MutationObserver(records => {
        const last = state.last;
        if (last === null || records.every(record => isOwn(record.target))) {
            return;
        }

        const target = document.elementFromPoint(last.clientX, last.clientY);
        if (!element.contains(target)) {
            state.leave();
            return;
        }

        const previous = state.target;
        state.target = null;
        state.track({ clientX: last.clientX, clientY: last.clientY, target });
        if ((previous !== null) && (state.target === null)) {
            hide(element);
        }
    });
    state.move = e => {
        if (element.dataset.trackball !== 'hover') {
            return;
        }

        if (state.last === null) {
            state.mutations.observe(element, { subtree: true, childList: true, attributes: true });
        }

        state.last = { clientX: e.clientX, clientY: e.clientY };
        state.track(e);
    };
    state.leave = () => {
        state.target = null;
        state.last = null;
        state.mutations.disconnect();
        members(element).forEach(hide);
    };
    element.addEventListener('pointermove', state.move);
    element.addEventListener('pointerdown', state.move);
    element.addEventListener('pointerleave', state.leave);
    element.addEventListener('pointercancel', state.leave);

    element.dataset.attached = '';
    return { dispose: () => detach(element, state) };
}

function detach(element, state) {
    state.observer.disconnect();
    state.mutations.disconnect();
    cancelAnimationFrame(state.frame);
    element.removeEventListener('pointermove', state.move);
    element.removeEventListener('pointerdown', state.move);
    element.removeEventListener('pointerleave', state.leave);
    element.removeEventListener('pointercancel', state.leave);

    hide(element);
    delete element.dataset.attached;
}

function isOwn(node) {
    const element = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
    return Boolean(element?.closest('.mussel-hover, .mussel-tooltip, .mussel-cross'));
}

function members(element) {
    const group = element.dataset.group;
    if (!group) {
        return [element];
    }

    return [...document.querySelectorAll('.mussel-chart')].filter(x => x.dataset.group === group);
}

function track(element, event) {
    const plot = element.querySelector('.mussel-plot');
    const ctm = plot?.getScreenCTM();
    if (!ctm) {
        return;
    }

    if (element.querySelector('.mussel-plot .mussel-series') === null) {
        showPoint(element, readSeries(element), ctm, event);
        return;
    }

    const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(ctm.inverse());
    const position = toPosition(element, point.x);
    for (const chart of members(element)) {
        show(chart, position, chart === element);
    }
}

function isPoints(item) {
    return item.kind === 'Scatter' || item.kind === 'Bubble';
}

function readCategories(chart) {
    const text = chart.dataset.categories;
    return text ? JSON.parse(text) : [];
}

export function toPosition(chart, x) {
    const kind = chart.dataset.xKind ?? 'time';
    if (kind === 'category') {
        const categories = readCategories(chart);
        const index = Math.min(Math.max(Math.round(x), 0), categories.length - 1);
        return { kind, value: categories[index], index, source: chart };
    }

    return { kind, value: Number(chart.querySelector('svg').dataset.base) + x };
}

export function fromPosition(chart, position) {
    if (position.kind === 'category') {
        return position.source === chart ? position.index : readCategories(chart).indexOf(position.value);
    }

    return position.value - Number(chart.querySelector('svg').dataset.base);
}

function valueBase(chart) {
    return Number(chart.querySelector('svg').dataset.valueBase);
}

export function readSeries(chart) {
    const base = valueBase(chart);
    return [...chart.querySelectorAll('.mussel-series')].map(group => {
        const values = new Map();
        for (const line of group.querySelectorAll('polyline')) {
            const originals = line.dataset.values?.split(' ');
            const pairs = (line.getAttribute('points') ?? '').split(' ');
            pairs.forEach((pair, index) => {
                const comma = pair.indexOf(',');
                if (comma > 0) {
                    const y = Number(pair.substring(comma + 1));
                    values.set(Number(pair.substring(0, comma)), { y, value: originals ? Number(originals[index]) : y + base });
                }
            });
        }

        const range = group.dataset.kind === 'RangeBar';
        for (const rect of group.querySelectorAll('.mussel-bars rect')) {
            values.set(Number(rect.getAttribute('x')), barEntry(rect, range, base));
        }

        for (const marker of group.querySelectorAll('.mussel-markers > *')) {
            values.set(Number(marker.dataset.x), markerEntry(marker, base));
        }

        return {
            title: group.dataset.title ?? '',
            color: group.dataset.color ?? '#808080',
            format: group.dataset.format ?? '',
            kind: group.dataset.kind ?? 'Line',
            group,
            values
        };
    });
}

function nearest(series, x) {
    let best = null;
    for (const item of series) {
        for (const key of item.values.keys()) {
            if (best === null || Math.abs(key - x) < Math.abs(best - x)) {
                best = key;
            }
        }
    }

    return best;
}

function show(chart, position, active) {
    const matrix = plotMatrix(chart.querySelector('.mussel-plot'));
    const series = readSeries(chart);
    if (!matrix || series.length === 0 || (chart.dataset.xKind ?? 'time') !== position.kind || series.every(isPoints)) {
        hide(chart);
        return;
    }

    const target = fromPosition(chart, position);
    const x = nearest(series, target);
    const horizontal = chart.dataset.orientation === 'horizontal';
    const clip = chart.querySelector('clipPath rect');
    const low = Number(clip.getAttribute(horizontal ? 'y' : 'x'));
    const high = low + Number(clip.getAttribute(horizontal ? 'height' : 'width'));
    const along = horizontal ? (matrix.b * x) + matrix.f : (matrix.a * x) + matrix.e;
    const tolerance = position.kind === 'category' ? 0 : spacing(series);
    if (x === null || along < low - 1 || along > high + 1 || (!active && Math.abs(x - target) > tolerance)) {
        hide(chart);
        return;
    }

    placeCross(chart, horizontal ? null : along, horizontal ? along : null);

    const hover = chart.querySelector('.mussel-hover');
    hover.replaceChildren();
    const rows = [];
    const culture = chart.dataset.culture;
    for (const item of series) {
        const entry = item.values.get(x);
        if (entry === undefined) {
            continue;
        }

        const dotX = entry.center ?? x;
        addDot(hover, (matrix.a * dotX) + (matrix.c * entry.y) + matrix.e, (matrix.b * dotX) + (matrix.d * entry.y) + matrix.f, item.color);
        rows.push([item.color, item.title, formatEntry(item, entry, culture)]);
    }

    const tooltip = chart.querySelector('.mussel-tooltip');
    if (chart.dataset.tooltip !== 'true' || (!active && chart.dataset.groupMode !== 'tooltip')) {
        tooltip.hidden = true;
        return;
    }

    fillTooltip(tooltip, formatPosition(chart, x), rows);
    const svg = chart.querySelector('svg');
    const scale = svg.getBoundingClientRect().width / Number(svg.getAttribute('width') || 1);
    if (horizontal) {
        const height = chart.getBoundingClientRect().height;
        let top = (along * scale) + 12;
        if (top + tooltip.offsetHeight > height) {
            top = (along * scale) - 12 - tooltip.offsetHeight;
        }

        tooltip.style.left = `${horizontalTooltipLeft(Number(chart.querySelector('.mussel-cross-across').getAttribute('x1')), scale, chart.getBoundingClientRect().width, tooltip.offsetWidth)}px`;
        tooltip.style.top = `${Math.max(top, 0)}px`;
        return;
    }

    const width = chart.getBoundingClientRect().width;
    let left = (along * scale) + 12;
    if (left + tooltip.offsetWidth > width) {
        left = (along * scale) - 12 - tooltip.offsetWidth;
    }

    tooltip.style.left = `${Math.max(left, 0)}px`;
    tooltip.style.top = '4px';
}

function showPoint(chart, series, ctm, event) {
    let best = null;
    let distance = Infinity;
    for (const item of series) {
        for (const marker of item.group.querySelectorAll('.mussel-markers > *')) {
            const x = Number(marker.dataset.x);
            const y = Number(marker.dataset.y);
            const dx = (ctm.a * x) + (ctm.c * y) + ctm.e - event.clientX;
            const dy = (ctm.b * x) + (ctm.d * y) + ctm.f - event.clientY;
            const current = (dx * dx) + (dy * dy);
            if (current < distance) {
                distance = current;
                best = { item, marker, x, y };
            }
        }
    }

    const matrix = plotMatrix(chart.querySelector('.mussel-plot'));
    if (best === null || !matrix) {
        hide(chart);
        return;
    }

    const px = (matrix.a * best.x) + (matrix.c * best.y) + matrix.e;
    const py = (matrix.b * best.x) + (matrix.d * best.y) + matrix.f;
    placeCross(chart, px, py);
    const hover = chart.querySelector('.mussel-hover');
    hover.replaceChildren();
    addDot(hover, px, py, best.item.color);

    const tooltip = chart.querySelector('.mussel-tooltip');
    if (chart.dataset.tooltip !== 'true') {
        tooltip.hidden = true;
        return;
    }

    fillTooltip(tooltip, formatPosition(chart, best.x), [[best.item.color, best.item.title, formatEntry(best.item, markerEntry(best.marker, valueBase(chart)), chart.dataset.culture)]]);
    const svg = chart.querySelector('svg');
    const scale = svg.getBoundingClientRect().width / Number(svg.getAttribute('width') || 1);
    placeTooltip(chart, tooltip, px * scale, py * scale);
}

function placeCross(chart, x, y) {
    const line = chart.querySelector('.mussel-cross:not(.mussel-cross-across)');
    const across = chart.querySelector('.mussel-cross-across');
    if (x !== null) {
        line.setAttribute('x1', x);
        line.setAttribute('x2', x);
    }

    if (y !== null) {
        across.setAttribute('y1', y);
        across.setAttribute('y2', y);
    }

    line.setAttribute('visibility', x !== null ? 'visible' : 'hidden');
    across.setAttribute('visibility', y !== null ? 'visible' : 'hidden');
}

function addDot(parent, x, y, color) {
    const dot = document.createElementNS(SVG_NS, 'circle');
    dot.setAttribute('cx', x);
    dot.setAttribute('cy', y);
    dot.setAttribute('r', '3.5');
    dot.setAttribute('fill', color);
    parent.appendChild(dot);
}

function formatEntry(item, entry, culture) {
    if (item.kind === 'RangeBar') {
        return `${formatValue(entry.low, item.format, culture)} – ${formatValue(entry.high, item.format, culture)}`;
    }

    const text = formatValue(entry.value, item.format, culture);
    return entry.size === undefined ? text : `${text} (${compact(Number(entry.size), culture)})`;
}

export function formatPosition(chart, x) {
    const kind = chart.dataset.xKind ?? 'time';
    if (kind === 'category') {
        return readCategories(chart)[x] ?? '';
    }

    const position = Number(chart.querySelector('svg').dataset.base) + x;
    if (kind === 'number') {
        return formatValue(position, chart.dataset.xFormat, chart.dataset.culture);
    }

    return formatTime(position, offsetAt(chart.dataset.offset, chart.dataset.offsetChanges, position), chart.dataset.timeFormat ?? 'HH:mm:ss');
}

function spacing(series) {
    const keys = [...series[0].values.keys()].sort((a, b) => a - b);
    let minimum = Infinity;
    for (let i = 1; i < keys.length; i++) {
        minimum = Math.min(minimum, keys[i] - keys[i - 1]);
    }

    return Number.isFinite(minimum) ? minimum : 1;
}

function trackItem(element, state, event) {
    const hit = event.pointerType === 'touch' ? document.elementFromPoint(event.clientX, event.clientY) : event.target;
    const target = hit?.closest?.('[data-tip]') ?? null;
    if (target === null || !element.contains(target)) {
        if (state.target !== null) {
            state.target = null;
            hide(element);
        }

        return;
    }

    if (target !== state.target) {
        state.target = target;
        showItem(element, target);
    }

    const bounds = element.getBoundingClientRect();
    const cross = element.querySelector('.mussel-cross');
    if (cross) {
        const svg = element.querySelector('svg');
        const x = (event.clientX - svg.getBoundingClientRect().left) * (Number(svg.getAttribute('width') || 1) / svg.getBoundingClientRect().width);
        cross.setAttribute('x1', x);
        cross.setAttribute('x2', x);
        cross.setAttribute('visibility', 'visible');
    }

    const tooltip = element.querySelector('.mussel-tooltip');
    if (!tooltip.hidden) {
        placeTooltip(element, tooltip, event.clientX - bounds.left, event.clientY - bounds.top);
    }
}

function showItem(chart, target) {
    const tip = JSON.parse(target.dataset.tip);
    const hover = chart.querySelector('.mussel-hover');
    hover?.replaceChildren();
    if (hover && tip.l) {
        const line = document.createElementNS(SVG_NS, 'line');
        line.setAttribute('class', 'mussel-mark-line');
        line.setAttribute('x1', tip.l[0]);
        line.setAttribute('y1', tip.l[1]);
        line.setAttribute('x2', tip.l[2]);
        line.setAttribute('y2', tip.l[3]);
        hover.appendChild(line);
    }

    if (hover && tip.m) {
        for (const [x, y, color] of tip.m) {
            addDot(hover, x, y, color);
        }
    }

    if (hover && target.dataset.mark) {
        const mark = document.createElementNS(SVG_NS, 'rect');
        mark.setAttribute('class', `mussel-mark-${target.dataset.mark}`);
        for (const name of ['x', 'y', 'width', 'height']) {
            mark.setAttribute(name, target.getAttribute(name));
        }

        hover.appendChild(mark);
    }

    const tooltip = chart.querySelector('.mussel-tooltip');
    if (chart.dataset.tooltip !== 'true') {
        tooltip.hidden = true;
        return;
    }

    fillTooltip(tooltip, tip.t, tip.r);
}

function fillTooltip(tooltip, title, rows) {
    tooltip.replaceChildren();
    if (title) {
        const header = document.createElement('div');
        header.className = 'mussel-tooltip-time';
        header.textContent = title;
        tooltip.appendChild(header);
    }

    for (const [color, name, value] of rows) {
        const line = document.createElement('div');
        line.className = 'mussel-tooltip-row';
        if (color) {
            const mark = document.createElement('i');
            mark.style.background = color;
            line.appendChild(mark);
        }

        const label = document.createElement('span');
        label.textContent = name;
        const text = document.createElement('b');
        text.textContent = value;
        line.append(label, text);
        tooltip.appendChild(line);
    }

    tooltip.hidden = false;
}

function placeTooltip(chart, tooltip, x, y) {
    const bounds = chart.getBoundingClientRect();
    let left = x + 12;
    if (left + tooltip.offsetWidth > bounds.width) {
        left = x - 12 - tooltip.offsetWidth;
    }

    let top = y + 12;
    if (top + tooltip.offsetHeight > bounds.height) {
        top = y - 12 - tooltip.offsetHeight;
    }

    tooltip.style.left = `${Math.max(left, 0)}px`;
    tooltip.style.top = `${Math.max(top, 0)}px`;
}

function hide(chart) {
    for (const cross of chart.querySelectorAll('.mussel-cross')) {
        cross.setAttribute('visibility', 'hidden');
    }

    chart.querySelector('.mussel-hover')?.replaceChildren();
    const tooltip = chart.querySelector('.mussel-tooltip');
    if (tooltip) {
        tooltip.hidden = true;
    }
}

function formatValue(value, format, culture) {
    let prefix = '';
    let suffix = '';
    let spec = format ?? '';
    const open = spec.indexOf('{');
    if (open >= 0) {
        const close = spec.indexOf('}', open);
        const inner = spec.substring(open + 1, close);
        const colon = inner.indexOf(':');
        prefix = spec.substring(0, open);
        suffix = spec.substring(close + 1);
        spec = colon >= 0 ? inner.substring(colon + 1) : '';
    }

    const kind = spec.charAt(0).toUpperCase();
    const digits = spec.length > 1 ? Number(spec.substring(1)) : 2;
    let text;
    if (kind === 'N' || kind === 'F') {
        text = numberFormat(culture, kind, digits).format(value);
    } else if (kind === 'P') {
        text = numberFormat(culture, 'P', digits).format(value);
    } else {
        text = compact(value, culture);
    }

    return prefix + text + suffix;
}

export function compact(value, culture) {
    const magnitude = Math.abs(value);
    if (magnitude < 0.005) {
        return '0';
    }

    if (magnitude < 10000) {
        const plain = roundDecimal(magnitude, 2);
        return numberFormat(culture, 'C', 2).format(value < 0 ? -plain : plain);
    }

    let unit = 0;
    let scaled = magnitude / 1000;
    while (scaled >= 1000 && unit < SUFFIXES.length - 1) {
        scaled /= 1000;
        unit++;
    }

    let rounded = roundCompact(scaled);
    if (rounded >= 1000 && unit < SUFFIXES.length - 1) {
        unit++;
        rounded = roundCompact(rounded / 1000);
    }

    const shown = Number(rounded.toPrecision(15));
    const text = numberFormat(culture, 'C', rounded >= 100 ? 0 : 2).format(value < 0 ? -shown : shown);
    return text + SUFFIXES[unit];
}

function numberFormat(culture, kind, digits) {
    const key = `${culture ?? ''}|${kind}|${digits}`;
    let format = numberFormats.get(key);
    if (format === undefined) {
        format = new Intl.NumberFormat(culture || undefined, kind === 'C'
            ? { maximumFractionDigits: digits }
            : kind === 'P'
                ? { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits }
                : { minimumFractionDigits: digits, maximumFractionDigits: digits, useGrouping: kind === 'N' });
        numberFormats.set(key, format);
    }

    return format;
}

function roundCompact(value) {
    return value >= 100 ? Math.round(value) : roundDecimal(value, value >= 10 ? 1 : 2);
}

function roundDecimal(value, digits) {
    return Number(`${Math.round(Number(`${value}e${digits}`))}e-${digits}`);
}

export function horizontalTooltipLeft(plotLeft, scale, width, tooltipWidth) {
    return Math.max(Math.min((plotLeft * scale) + 8, width - tooltipWidth), 0);
}

function markerEntry(marker, base) {
    const y = Number(marker.dataset.y);
    return { y, value: y + base, size: marker.dataset.size };
}

export function barEntry(rect, range, base) {
    const value = Number(rect.dataset.v);
    const center = Number(rect.getAttribute('x')) + Number(rect.parentNode.dataset.offset);
    const low = Number(rect.getAttribute('y'));
    if (range) {
        return { y: value - base, value, low: low + base, high: value, center };
    }

    const high = low + Number(rect.getAttribute('height'));
    return { y: value < 0 ? low : high, value, center };
}

export function plotMatrix(plot) {
    const list = plot?.transform.baseVal;
    return list?.numberOfItems > 0 ? list.getItem(0).matrix : null;
}

export function offsetAt(offset, changes, seconds) {
    const list = changes ? changes.split(' ') : [];
    let low = 0;
    let high = list.length;
    while (low < high) {
        const middle = (low + high) >> 1;
        if (seconds < Number(list[middle].split(',')[0])) {
            high = middle;
        } else {
            low = middle + 1;
        }
    }

    return low < list.length ? Number(list[low].split(',')[1]) : Number(offset ?? 0);
}

export function formatTime(seconds, offset, format) {
    const date = new Date((seconds + (offset * 60)) * 1000);
    const pad = (n, width) => String(n).padStart(width, '0');
    return format.replace(/yyyy|HH|mm|ss|M|d/g, token => {
        switch (token) {
            case 'yyyy': return String(date.getUTCFullYear());
            case 'HH': return pad(date.getUTCHours(), 2);
            case 'mm': return pad(date.getUTCMinutes(), 2);
            case 'ss': return pad(date.getUTCSeconds(), 2);
            case 'M': return String(date.getUTCMonth() + 1);
            default: return String(date.getUTCDate());
        }
    });
}
