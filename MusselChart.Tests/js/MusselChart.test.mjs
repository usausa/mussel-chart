import { test } from 'node:test';
import assert from 'node:assert/strict';
import { barEntry, compact, formatPosition, formatTime, fromPosition, horizontalTooltipLeft, markerEntry, offsetAt, plotMatrix, readSeries, toPosition } from '../../MusselChart/wwwroot/MusselChart.js';

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

test('a range bar takes its low end from the rect and puts the dot on the high end', () => {
    assert.deepEqual(barEntry(rect({ x: '1', y: '12345.67', height: '4.45' }, { v: '12350.12' }), true, 0), { y: 12350.12, value: 12350.12, low: 12345.67, high: 12350.12, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '-10', height: '5' }, { v: '-5' }), true, 0), { y: -5, value: -5, low: -10, high: -5, center: 1.5 });
});

test('a range bar shows its ends with the value base, and every bar keeps its dot in the plot', () => {
    assert.deepEqual(barEntry(rect({ x: '2', y: '-3', height: '3' }, { v: '-3' }), false, 0), { y: -3, value: -3, center: 2.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '-10', height: '75' }, { v: '1234567955' }), true, 1234567890), { y: 65, value: 1234567955, low: 1234567880, high: 1234567955, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '-1234567890', height: '1234567955' }, { v: '1234567955' }), false, 1234567890), { y: 65, value: 1234567955, center: 1.5 });
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

test('a marker shows its value with the value base', () => {
    assert.deepEqual(markerEntry({ dataset: { y: '10', size: '2' } }, 1234567890), { y: 10, value: 1234567900, size: '2' });
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
