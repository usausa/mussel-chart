import { test } from 'node:test';
import assert from 'node:assert/strict';
import { compact } from '../../MusselChart/wwwroot/MusselChart.js';

test('短縮表記は、丸めた後に次の単位へ繰り上げる', () => {
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
