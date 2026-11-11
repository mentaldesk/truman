import { describe, expect, it } from 'vitest';
import { formatStoryAge } from './storyAge';

const now = new Date(2026, 10, 10, 12, 0, 0);
const ago = (ms: number) => new Date(now.getTime() - ms).toISOString();

describe('formatStoryAge', () => {
	it('shows minutes under an hour', () => {
		expect(formatStoryAge(ago(12 * 60_000), now)).toBe('12 min ago');
	});

	it('shows hours under a day', () => {
		expect(formatStoryAge(ago(3 * 3_600_000), now)).toBe('3 hours ago');
		expect(formatStoryAge(ago(3_600_000), now)).toBe('1 hour ago');
		expect(formatStoryAge(ago(23 * 3_600_000 + 59 * 60_000), now)).toBe('23 hours ago');
	});

	it('shows the date from a day old in this year', () => {
		expect(formatStoryAge(new Date(2026, 10, 7, 9, 0).toISOString(), now)).toBe('7 Nov');
	});

	it('adds the year for an earlier year', () => {
		expect(formatStoryAge(new Date(2025, 1, 14, 9, 0).toISOString(), now)).toBe('14 Feb 2025');
	});

	it('shows just now for a future date', () => {
		expect(formatStoryAge(new Date(now.getTime() + 5 * 3_600_000).toISOString(), now)).toBe('Just now');
	});

	it('shows nothing when the date is missing', () => {
		expect(formatStoryAge(null, now)).toBeNull();
		expect(formatStoryAge(undefined, now)).toBeNull();
		expect(formatStoryAge('not a date', now)).toBeNull();
	});
});
