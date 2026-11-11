import { page } from 'vitest/browser';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import SavedPage from './+page.svelte';

const story = (id: number, title: string) => ({
	id,
	link: `https://example.invalid/${id}`,
	title,
	tldr: `${title} in brief`,
	content: 'Body',
	sentiment: 8,
	tags: [],
	relevanceScore: 0.5,
	createdAt: new Date().toISOString(),
	publisher: 'RNZ World',
	publishedAt: null,
	why: { valuesShare: 1, tagsShare: 0, toneShare: 0, matchedValues: [], matchedTags: [], mood: 5 },
	isSaved: true
});

function serve(saved: ReturnType<typeof story>[]) {
	vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
		const path = String(url);
		const method = init?.method ?? 'GET';
		const id = Number(path.match(/articles\/(\d+)\/saved/)?.[1]);
		if (method === 'DELETE') saved = saved.filter((s) => s.id !== id);
		if (path.includes('/api/articles/saved')) return Response.json({ articles: saved });
		if (path.includes('tag-preferences')) return Response.json([]);
		return new Response(null, { status: 204 });
	});
}

afterEach(() => vi.unstubAllGlobals());

describe('/saved', () => {
	it('says how to save a story when nothing is saved', async () => {
		serve([]);
		render(SavedPage);
		await expect
			.element(page.getByText('Nothing saved yet. Tap the bookmark on a story to keep it here.'))
			.toBeInTheDocument();
	});

	it('lists saved stories in the order the API gives them', async () => {
		serve([story(2, 'Newer'), story(1, 'Older')]);
		render(SavedPage);
		await expect.element(page.getByText('Newer', { exact: true })).toBeInTheDocument();
		const titles = [...document.querySelectorAll('main .text-xl')].map((e) => e.textContent?.trim());
		expect(titles).toEqual(['Newer', 'Older']);
	});

	it('opens a story and un-saving it takes it off the list', async () => {
		serve([story(2, 'Newer'), story(1, 'Older')]);
		render(SavedPage);
		await page.getByText('Older', { exact: true }).click();
		await expect.element(page.getByRole('button', { name: 'Remove from saved' })).toBeInTheDocument();

		await page.getByRole('button', { name: 'Remove from saved' }).click();
		await expect.element(page.getByRole('button', { name: 'Save', exact: true })).toBeInTheDocument();
		await page.getByRole('button', { name: 'Close' }).click();

		await expect.element(page.getByText('Newer', { exact: true })).toBeInTheDocument();
		await expect.element(page.getByText('Older', { exact: true })).not.toBeInTheDocument();
	});
});
