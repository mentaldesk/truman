import { describe, expect, it } from 'vitest';
import { refreshed } from './refreshed';

describe('refreshed', () => {
	it('returns the refetched copy of the item', () => {
		const before = { id: 2, tags: ['space'] };
		const after = { id: 2, tags: ['space', 'favourite'] };
		expect(refreshed(before, [{ id: 1, tags: [] }, after])).toBe(after);
	});

	it('returns null when the item has left the list', () => {
		expect(refreshed({ id: 2 }, [{ id: 1 }])).toBeNull();
	});

	it('returns null when nothing was selected', () => {
		expect(refreshed(null, [{ id: 1 }])).toBeNull();
	});
});
