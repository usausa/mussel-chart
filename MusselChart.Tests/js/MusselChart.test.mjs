import { test } from 'node:test';
import assert from 'node:assert/strict';
import { attach, barEntry, clipBounds, compact, fillTooltip, formatPosition, formatTime, fromPosition, horizontalTooltipLeft, hoverEntries, isSafeColor, nearestMarker, offsetAt, plotMatrix, readSeries, toPosition } from '../../MusselChart/wwwroot/MusselChart.js';

const parentNode = { dataset: { offset: '0.5' } };
const rect = (attributes, dataset) => ({ getAttribute: (name) => attributes[name], dataset, parentNode });

test('compact notation moves to the next unit after rounding', () => {
    const cases = [
        [0.004, '0'],
        [1234.567, '1,234.57'],
        [2.675, '2.68'],
        [1.005, '1.01'],
        [-2.675, '-2.68'],
        [12350, '12.4K'],
        [-12350, '-12.4K'],
        [9999.999, '10,000'],
        [12345, '12.3K'],
        [99995, '100K'],
        [999499, '999K'],
        [999999, '1M'],
        [-999999, '-1M'],
        [1234567, '1.23M'],
        [999999999, '1B'],
        [1e15, '1,000T'],
        [1e33, '1,000,000,000,000,000,000,000T'],
        [1e34, '10,000,000,000,000,000,000,000T'],
    ];
    for (const [value, expected] of cases) {
        assert.equal(compact(value, 'en-US'), expected, String(value));
    }
});

test('compact notation groups the thousands of four digits in every culture', () => {
    assert.equal(compact(1234.5, 'es-ES'), '1.234,5');
    assert.equal(compact(-1234.5, 'es-ES'), '-1.234,5');
    assert.equal(compact(1e15, 'es-ES'), '1.000T');
});

test('hover dots leave out the points outside the clip and keep their rows', () => {
    const matrix = { a: 10, b: 0, c: 0, d: -1, e: 50, f: 200 };
    const bounds = { left: 40, top: 8, right: 600, bottom: 212 };
    const series = [
        { title: 'in', color: '#111', format: '', kind: 'Line', values: new Map([[1, { y: 100, value: 100 }]]) },
        { title: 'out', color: '#222', format: '', kind: 'Line', values: new Map([[1, { y: 250, value: 250 }]]) },
    ];

    const { dots, rows } = hoverEntries(series, 1, matrix, bounds, 'en-US');

    assert.deepEqual(dots, [[60, 100, '#111']]);
    assert.deepEqual(rows, [['#111', 'in', '100'], ['#222', 'out', '250']]);
});

test('the nearest marker is chosen among the markers inside the clip', () => {
    const marker = (x, y) => ({ dataset: { x: String(x), y: String(y) } });
    const inside = marker(2, 50);
    const outside = marker(1, 150);
    const matrix = { a: 10, b: 0, c: 0, d: -1, e: 0, f: 100 };
    const bounds = { left: 0, top: 0, right: 100, bottom: 100 };

    assert.equal(nearestMarker([{ group: { querySelectorAll: () => [outside, inside] } }], matrix, matrix, bounds, 10, -50).marker, inside);
    assert.equal(nearestMarker([{ group: { querySelectorAll: () => [outside] } }], matrix, matrix, bounds, 10, -50), null);
});

test('a marker drawn past an end of the plot is neither found nor read', () => {
    const inside = { dataset: { x: '2', y: '50' } };
    const past = { dataset: { x: '1', y: '50', outside: 'true' } };
    const matrix = { a: 10, b: 0, c: 0, d: -1, e: 0, f: 100 };
    const bounds = { left: 0, top: 0, right: 100, bottom: 100 };
    const group = {
        dataset: { title: 'p', color: '#fff', format: '', kind: 'Bubble' },
        querySelectorAll: (selector) => (selector === '.mussel-markers > *' ? [past, inside] : []),
    };
    const chart = { querySelectorAll: () => [group], querySelector: () => ({ dataset: { valueBase: '0' } }) };

    assert.equal(nearestMarker([{ group }], matrix, matrix, bounds, 10, -50).marker, inside);
    assert.equal(nearestMarker([{ group: { querySelectorAll: () => [past] } }], matrix, matrix, bounds, 10, -50), null);
    assert.deepEqual([...readSeries(chart)[0].values.keys()], [2]);
});

test('colors of styles keep the safe characters and leave out urls', () => {
    for (const color of ['#5794F2', 'rgb(87 148 242 / 80%)', 'var(--brand)', 'a'.repeat(64)]) {
        assert.equal(isSafeColor(color), true, color);
    }

    for (const color of ['url(//example.com/x.png)', 'URL(x)', 'a'.repeat(65), '', 'red; position: fixed', null]) {
        assert.equal(isSafeColor(color), false, String(color));
    }
});

test('a tooltip row whose color is not safe has no mark', () => {
    const saved = globalThis.document;
    globalThis.document = {
        createElement: (name) => ({ name, style: {}, children: [], appendChild(child) { this.children.push(child); }, append(...items) { this.children.push(...items); } }),
    };
    try {
        const tooltip = { children: [], hidden: true, replaceChildren() { this.children = []; }, appendChild(child) { this.children.push(child); } };

        fillTooltip(tooltip, null, [['#123456', 'a', '1'], ['red;background:url(x)', 'b', '2']]);

        assert.deepEqual(tooltip.children.map((row) => row.children.filter((child) => child.name === 'i').map((child) => child.style.background)), [['#123456'], []]);
        assert.equal(tooltip.hidden, false);
    } finally {
        globalThis.document = saved;
    }
});

test('the clip bounds come from the rect of the clip path', () => {
    const attributes = { x: '40', y: '6', width: '590', height: '216' };
    const chart = { querySelector: (selector) => (selector === 'clipPath rect' ? { getAttribute: (name) => attributes[name] } : null) };

    assert.deepEqual(clipBounds(chart), { left: 40, top: 6, right: 630, bottom: 222 });
});

test('times are written as Unix seconds plus the time zone offset', () => {
    assert.equal(formatTime(1803859200, 0, 'M/d'), '3/1');
    assert.equal(formatTime(1803859200, 540, 'yyyy-M-d HH:mm'), '2027-3-1 09:00');
});

test('a time before an offset change uses the earlier offset', () => {
    assert.equal(offsetAt('60', '1792890000,120', 1792889999), 120);
    assert.equal(offsetAt('60', '1792890000,120', 1792890000), 60);
    assert.equal(offsetAt('540', undefined, 1792889999), 540);
});

test('a time between two offset changes uses the offset of its period', () => {
    const changes = '1772953200,-300 1793512800,-240';
    assert.equal(offsetAt('-300', changes, 1772953199), -300);
    assert.equal(offsetAt('-300', changes, 1772953200), -240);
    assert.equal(offsetAt('-300', changes, 1793512799), -240);
    assert.equal(offsetAt('-300', changes, 1793512800), -300);
});

test('the root loses its mark when the script is disposed', () => {
    const saved = [globalThis.ResizeObserver, globalThis.MutationObserver, globalThis.cancelAnimationFrame];
    globalThis.ResizeObserver = class { observe() { } disconnect() { } };
    globalThis.MutationObserver = class { observe() { } disconnect() { } };
    globalThis.cancelAnimationFrame = () => { };
    try {
        const element = { dataset: {}, addEventListener() { }, removeEventListener() { }, querySelectorAll: () => [], querySelector: () => null };

        const attached = attach(element, null);
        const marked = Object.hasOwn(element.dataset, 'attached');
        attached.dispose();

        assert.deepEqual([marked, Object.hasOwn(element.dataset, 'attached')], [true, false]);
    } finally {
        [globalThis.ResizeObserver, globalThis.MutationObserver, globalThis.cancelAnimationFrame] = saved;
    }
});

test('a range bar takes its low end from the rect and puts the dot on the high end', () => {
    assert.deepEqual(barEntry(rect({ x: '1', y: '12345.67', height: '4.45' }, { v: '12350.12' }), true, 0), { y: 12350.12, value: 12350.12, low: 12345.67, high: 12350.12, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '0', height: '7' }, { v: '-5' }), true, -12), { y: 7, value: -5, low: -12, high: -5, center: 1.5 });
});

test('a range bar shows its ends with the value base, and every bar keeps its dot in the plot', () => {
    assert.deepEqual(barEntry(rect({ x: '2', y: '-3', height: '3' }, { v: '-3' }), false, 0), { y: -3, value: -3, center: 2.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '0', height: '75' }, { v: '1234567955' }), true, 1234567880), { y: 75, value: 1234567955, low: 1234567880, high: 1234567955, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '-210', height: '275' }, { v: '1234567955' }), false, 1234567890), { y: 65, value: 1234567955, center: 1.5 });
});

test('a numeric position adds the base of its chart', () => {
    const chart = (base) => ({ dataset: { xKind: 'number' }, querySelector: () => ({ dataset: { base } }) });
    const position = toPosition(chart('1000000'), 2);
    assert.deepEqual(position, { kind: 'number', value: 1000002 });
    assert.equal(fromPosition(chart('999990'), position), 12);
});

test('series values add the value base unless the line keeps its own values', () => {
    const element = (attributes, dataset = {}) => ({ getAttribute: (name) => attributes[name] ?? null, dataset });
    const lines = [element({ points: '0,0 1,65' }), element({ points: '2,3' }, { values: '7' })];
    const markers = [element({}, { x: '3', y: '10', size: '2' })];
    const group = {
        dataset: { title: 'a', color: '#fff', format: '', kind: 'Line' },
        querySelectorAll: (selector) => (selector === 'polyline' ? lines : (selector === '.mussel-markers > *' ? markers : [])),
    };
    const chart = { querySelectorAll: () => [group], querySelector: () => ({ dataset: { valueBase: '1234567890' } }) };

    const [series] = readSeries(chart);

    assert.deepEqual([...series.values], [[0, { y: 0, value: 1234567890 }], [1, { y: 65, value: 1234567955 }], [2, { y: 3, value: 7 }], [3, { y: 10, value: 1234567900, size: '2' }]]);
});

test('a series of range bars reads its bars as ranges', () => {
    const bars = [rect({ x: '1', y: '0', height: '5' }, { v: '-5' })];
    const group = {
        dataset: { title: 'r', color: '#fff', format: '', kind: 'RangeBar' },
        querySelectorAll: (selector) => (selector === '.mussel-bars rect' ? bars : []),
    };
    const chart = { querySelectorAll: () => [group], querySelector: () => ({ dataset: { valueBase: '-10' } }) };

    const [series] = readSeries(chart);

    assert.deepEqual([...series.values], [[1, { y: 5, value: -5, low: -10, high: -5, center: 1.5 }]]);
});

test('a position is written with the base of its chart, and a time with the offset of its period', () => {
    const chart = (dataset, base) => ({ dataset, querySelector: () => ({ dataset: { base } }) });
    assert.equal(formatPosition(chart({ xKind: 'number', xFormat: 'N0', culture: 'en-US' }, '1000000'), 2), '1,000,002');
    const time = chart({ offset: '60', offsetChanges: '1792890000,120', timeFormat: 'HH:mm:ss' }, '1792889000');
    assert.equal(formatPosition(time, 999), '02:59:59');
    assert.equal(formatPosition(time, 1000), '02:00:00');
});

test('a horizontal tooltip starts at the plot and stays inside the chart', () => {
    assert.equal(horizontalTooltipLeft(40, 1, 640, 100), 48);
    assert.equal(horizontalTooltipLeft(40, 2, 640, 100), 88);
    assert.equal(horizontalTooltipLeft(40, 1, 120, 100), 20);
    assert.equal(horizontalTooltipLeft(40, 1, 80, 100), 0);
});

test('a chart finds its own category by its place even when names repeat', () => {
    const chart = { dataset: { xKind: 'category', categories: '["","",""]' } };
    const other = { dataset: { xKind: 'category', categories: '["","",""]' } };
    const position = toPosition(chart, 2);
    assert.equal(fromPosition(chart, position), 2);
    assert.equal(fromPosition(other, position), 0);
    const named = { dataset: { xKind: 'category', categories: '["a","b","c"]' } };
    const partner = { dataset: { xKind: 'category', categories: '["c","b","a"]' } };
    assert.equal(fromPosition(partner, toPosition(named, 2)), 0);
});

test('the plot matrix is read without rewriting the transform', () => {
    const matrix = { a: 2, d: -3 };
    const baseVal = { numberOfItems: 1, getItem: () => ({ matrix }), consolidate: () => assert.fail('consolidate rewrites the attribute in Firefox') };
    assert.equal(plotMatrix({ transform: { baseVal } }), matrix);
    assert.equal(plotMatrix({ transform: { baseVal: { numberOfItems: 0 } } }), null);
    assert.equal(plotMatrix(null), null);
});
