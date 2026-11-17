import { test } from 'node:test';
import assert from 'node:assert/strict';
import { compact, formatTime, offsetAt } from '../../MusselChart/wwwroot/MusselChart.js';

test('compact notation moves to the next unit after rounding', () => {
    const cases = [
        [0.004, '0'],
        [1234.567, '1,234.57'],
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
