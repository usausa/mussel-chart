const charts = new WeakMap();
const ns = 'http://www.w3.org/2000/svg';
const suffixes = ['K', 'M', 'B', 'T'];

export function attach(element, reference) {
    const state = { width: 0, frame: 0 };
    state.observer = new ResizeObserver(entries => {
        const width = Math.floor(entries[0].contentRect.width);
        if (width <= 0 || width === state.width) {
            return;
        }

        state.width = width;
        cancelAnimationFrame(state.frame);
        state.frame = requestAnimationFrame(() => reference.invokeMethodAsync('OnResize', width).catch(() => { }));
    });
    state.observer.observe(element);

    if (element.dataset.trackball === 'hover') {
        state.move = e => track(element, e.clientX);
        state.leave = () => members(element).forEach(hide);
        element.addEventListener('pointermove', state.move);
        element.addEventListener('pointerdown', state.move);
        element.addEventListener('pointerleave', state.leave);
        element.addEventListener('pointercancel', state.leave);
    }

    charts.set(element, state);
}

export function detach(element) {
    const state = charts.get(element);
    if (!state) {
        return;
    }

    state.observer.disconnect();
    cancelAnimationFrame(state.frame);
    if (state.move) {
        element.removeEventListener('pointermove', state.move);
        element.removeEventListener('pointerdown', state.move);
        element.removeEventListener('pointerleave', state.leave);
        element.removeEventListener('pointercancel', state.leave);
    }

    hide(element);
    charts.delete(element);
}

function members(element) {
    const group = element.dataset.group;
    if (!group) {
        return [element];
    }

    return [...document.querySelectorAll('.mussel-chart')].filter(x => x.dataset.group === group);
}

function track(element, clientX) {
    const plot = element.querySelector('.mussel-plot');
    const ctm = plot?.getScreenCTM();
    if (!ctm) {
        return;
    }

    const base = Number(element.querySelector('svg').dataset.base);
    const point = new DOMPoint(clientX, 0).matrixTransform(ctm.inverse());
    const seconds = base + point.x;
    for (const chart of members(element)) {
        show(chart, seconds, chart === element);
    }
}

function readSeries(chart) {
    return [...chart.querySelectorAll('.mussel-series')].map(group => {
        const values = new Map();
        for (const line of group.querySelectorAll('polyline')) {
            const text = line.getAttribute('points') ?? '';
            for (const pair of text.split(' ')) {
                const comma = pair.indexOf(',');
                if (comma > 0) {
                    values.set(Number(pair.substring(0, comma)), Number(pair.substring(comma + 1)));
                }
            }
        }

        for (const rect of group.querySelectorAll('rect')) {
            const x = Number(rect.getAttribute('x')) + (Number(rect.getAttribute('width')) / 2);
            const y = Number(rect.getAttribute('y'));
            const height = Number(rect.getAttribute('height'));
            values.set(Math.round(x * 1000) / 1000, y < 0 ? y : y + height);
        }

        return {
            title: group.dataset.title ?? '',
            color: group.dataset.color ?? '#808080',
            format: group.dataset.format ?? '',
            stack: group.dataset.stack ?? '',
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

function show(chart, seconds, active) {
    const svg = chart.querySelector('svg');
    const plot = chart.querySelector('.mussel-plot');
    const matrix = plot?.transform.baseVal.consolidate()?.matrix;
    const series = readSeries(chart);
    if (!matrix || series.length === 0) {
        hide(chart);
        return;
    }

    const base = Number(svg.dataset.base);
    const x = nearest(series, seconds - base);
    const clip = chart.querySelector('clipPath rect');
    const left = Number(clip.getAttribute('x'));
    const right = left + Number(clip.getAttribute('width'));
    const px = (matrix.a * x) + matrix.e;
    if (x === null || px < left - 1 || px > right + 1 || (!active && Math.abs(base + x - seconds) > spacing(series))) {
        hide(chart);
        return;
    }

    const cross = chart.querySelector('.mussel-cross');
    cross.setAttribute('x1', px);
    cross.setAttribute('x2', px);
    cross.setAttribute('visibility', 'visible');

    const hover = chart.querySelector('.mussel-hover');
    hover.replaceChildren();
    const rows = [];
    const stacks = new Map();
    for (const item of series) {
        const raw = item.values.get(x);
        if (raw === undefined) {
            continue;
        }

        let value = raw;
        if (item.stack) {
            const below = stacks.get(item.stack) ?? 0;
            value = raw - below;
            stacks.set(item.stack, raw);
        }

        const dot = document.createElementNS(ns, 'circle');
        dot.setAttribute('cx', px);
        dot.setAttribute('cy', (matrix.d * raw) + matrix.f);
        dot.setAttribute('r', '3.5');
        dot.setAttribute('fill', item.color);
        hover.appendChild(dot);
        rows.push({ title: item.title, color: item.color, text: formatValue(value, item.format, chart.dataset.culture) });
    }

    const tooltip = chart.querySelector('.mussel-tooltip');
    if (chart.dataset.tooltip !== 'true' || (!active && chart.dataset.groupMode !== 'tooltip')) {
        tooltip.hidden = true;
        return;
    }

    const time = document.createElement('div');
    time.className = 'mussel-tooltip-time';
    time.textContent = formatTime(base + x, Number(chart.dataset.offset ?? 0), chart.dataset.timeFormat ?? 'HH:mm:ss');
    tooltip.replaceChildren(time);
    for (const row of rows) {
        const line = document.createElement('div');
        line.className = 'mussel-tooltip-row';
        const mark = document.createElement('i');
        mark.style.background = row.color;
        const name = document.createElement('span');
        name.textContent = row.title;
        const text = document.createElement('b');
        text.textContent = row.text;
        line.append(mark, name, text);
        tooltip.appendChild(line);
    }

    tooltip.hidden = false;
    const scale = svg.getBoundingClientRect().width / Number(svg.getAttribute('width') || 1);
    const width = chart.getBoundingClientRect().width;
    let position = (px * scale) + 12;
    if (position + tooltip.offsetWidth > width) {
        position = (px * scale) - 12 - tooltip.offsetWidth;
    }

    tooltip.style.left = `${Math.max(position, 0)}px`;
    tooltip.style.top = '4px';
}

function spacing(series) {
    const keys = [...series[0].values.keys()].sort((a, b) => a - b);
    let minimum = Infinity;
    for (let i = 1; i < keys.length; i++) {
        minimum = Math.min(minimum, keys[i] - keys[i - 1]);
    }

    return Number.isFinite(minimum) ? minimum : 1;
}

function hide(chart) {
    chart.querySelector('.mussel-cross')?.setAttribute('visibility', 'hidden');
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
        text = new Intl.NumberFormat(culture || undefined, { minimumFractionDigits: digits, maximumFractionDigits: digits, useGrouping: kind === 'N' }).format(value);
    } else if (kind === 'P') {
        text = new Intl.NumberFormat(culture || undefined, { style: 'percent', minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
    } else {
        text = compact(value, culture);
    }

    return prefix + text + suffix;
}

function compact(value, culture) {
    const magnitude = Math.abs(value);
    if (magnitude < 0.005) {
        return '0';
    }

    if (magnitude < 10000) {
        return new Intl.NumberFormat(culture || undefined, { maximumFractionDigits: 2 }).format(value);
    }

    let unit = 0;
    let scaled = magnitude / 1000;
    while (scaled >= 1000 && unit < suffixes.length - 1) {
        scaled /= 1000;
        unit++;
    }

    const digits = scaled >= 100 ? 0 : scaled >= 10 ? 1 : 2;
    const text = new Intl.NumberFormat(culture || undefined, { maximumFractionDigits: digits }).format(value < 0 ? -scaled : scaled);
    return text + suffixes[unit];
}

function formatTime(seconds, offset, format) {
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
