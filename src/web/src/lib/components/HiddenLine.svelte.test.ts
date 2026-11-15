import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import HiddenLine from './HiddenLine.svelte';

const line = () => document.querySelector('p')?.textContent?.replace(/\s+/g, ' ').trim();

describe('HiddenLine', () => {
	it('says how many stories were below the mood', async () => {
		render(HiddenLine, { hidden: { belowMood: 9, bannedTag: 0, mood: 7 } });
		await expect.element(page.getByText(/Not shown/)).toBeInTheDocument();
		expect(line()).toBe('Not shown: 9 below your mood (7)');
		expect(document.querySelector('a')).toBeNull();
	});

	it('links a banned tag to the Tags page', async () => {
		render(HiddenLine, { hidden: { belowMood: 0, bannedTag: 3, mood: 7 } });
		await expect.element(page.getByText(/Not shown/)).toBeInTheDocument();
		expect(line()).toBe('Not shown: 3 with a banned tag');
		await expect.element(page.getByRole('link', { name: 'a banned tag' })).toHaveAttribute('href', '/tags');
	});

	it('joins both parts', async () => {
		render(HiddenLine, { hidden: { belowMood: 9, bannedTag: 3, mood: 7 } });
		await expect.element(page.getByText(/Not shown/)).toBeInTheDocument();
		expect(line()).toBe('Not shown: 9 below your mood (7) · 3 with a banned tag');
	});

	it('shows nothing when nothing was hidden', async () => {
		render(HiddenLine, { hidden: { belowMood: 0, bannedTag: 0, mood: 7 } });
		expect(document.querySelector('p')).toBeNull();
	});
});
