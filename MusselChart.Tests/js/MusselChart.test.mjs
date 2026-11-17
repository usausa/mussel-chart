import { test } from 'node:test';
import assert from 'node:assert/strict';
import { barEntry, compact, formatTime, fromPosition, horizontalTooltipLeft, offsetAt, plotMatrix, toPosition } from '../../MusselChart/wwwroot/MusselChart.js';

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

test('a range bar takes both ends from its data and puts the dot on the high end', () => {
    const parentNode = { dataset: { offset: '0.5' } };
    const rect = (attributes, dataset) => ({ getAttribute: (name) => attributes[name], dataset, parentNode });
    assert.deepEqual(barEntry(rect({ x: '1', y: '12345.7', height: '4.45' }, { v: '12350.12', low: '12345.67' })), { y: 12350.12, value: 12350.12, low: 12345.67, high: 12350.12, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '1', y: '-10', height: '5' }, { v: '-5', low: '-10' })), { y: -5, value: -5, low: -10, high: -5, center: 1.5 });
    assert.deepEqual(barEntry(rect({ x: '2', y: '-3', height: '3' }, { v: '-3' })), { y: -3, value: -3, low: -3, high: 0, center: 2.5 });
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
