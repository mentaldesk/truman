import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import MatchBadges from './MatchBadges.svelte';

const badgeTexts = () => page.getByRole('listitem').elements().map(e => e.textContent?.trim());

describe('MatchBadges', () => {
	it('shows tags before values, values by name', async () => {
		render(MatchBadges, { matchedTags: ['nz'], matchedValues: ['peace-of-mind'] });
		await expect.element(page.getByText('Peace of Mind')).toBeInTheDocument();
		expect(badgeTexts()).toEqual(['nz', 'Peace of Mind']);
	});

	it('tints tags and values differently', async () => {
		render(MatchBadges, { matchedTags: ['security'], matchedValues: ['security'] });
		const tag = page.getByText('security', { exact: true });
		const value = page.getByText('Security', { exact: true });
		await expect.element(tag).toHaveAttribute('data-kind', 'tag');
		await expect.element(value).toHaveAttribute('data-kind', 'value');
		expect(tag.element().className).not.toEqual(value.element().className);
	});

	it('shows three badges, then +N for the rest', async () => {
		render(MatchBadges, { matchedTags: ['nz', 'science'], matchedValues: ['security', 'knowledge', 'freedom'] });
		await expect.element(page.getByText('+2')).toBeInTheDocument();
		expect(badgeTexts()).toEqual(['nz', 'science', 'Security', '+2']);
	});

	it('renders no row when nothing matched', async () => {
		render(MatchBadges, { matchedTags: [], matchedValues: [] });
		await expect.element(page.getByRole('list')).not.toBeInTheDocument();
	});
});
