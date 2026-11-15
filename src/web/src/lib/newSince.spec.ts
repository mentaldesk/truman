import { describe, expect, it } from 'vitest';
import { formatVisitTime } from './newSince';

const now = new Date(2026, 10, 10, 14, 0, 0);
const at = (day: number, hours: number, minutes: number) => new Date(2026, 10, day, hours, minutes).toISOString();

describe('formatVisitTime', () => {
	it('says this morning for earlier today', () => {
		expect(formatVisitTime(at(10, 7, 40), now)).toBe('7:40 this morning');
	});

	it('says this afternoon and this evening later today', () => {
		expect(formatVisitTime(at(10, 13, 5), now)).toBe('1:05 this afternoon');
		expect(formatVisitTime(at(10, 19, 30), new Date(2026, 10, 10, 22, 0))).toBe('7:30 this evening');
	});

	it('says last night for late yesterday and the small hours', () => {
		expect(formatVisitTime(at(9, 21, 15), now)).toBe('9:15 last night');
		expect(formatVisitTime(at(10, 0, 30), now)).toBe('12:30 last night');
	});

	it('says yesterday at a 24-hour time for earlier yesterday', () => {
		expect(formatVisitTime(at(9, 18, 2), now)).toBe('yesterday at 18:02');
		expect(formatVisitTime(at(9, 9, 5), now)).toBe('yesterday at 09:05');
	});

	it('gives a date when it is longer ago', () => {
		expect(formatVisitTime(at(7, 9, 0), now)).toBe('7 Nov');
		expect(formatVisitTime(new Date(2025, 1, 14, 9, 0).toISOString(), now)).toBe('14 Feb 2025');
	});
});
