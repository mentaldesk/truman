import { page } from 'vitest/browser';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render } from 'vitest-browser-svelte';
import ArticleDetailsPopup from './ArticleDetailsPopup.svelte';

const story = (isSaved: boolean) => ({
	id: 42,
	link: 'https://example.invalid/story',
	title: 'Flood barriers held',
	tldr: '',
	content: 'Body',
	sentiment: 8,
	tags: [],
	relevanceScore: 0.5,
	createdAt: new Date().toISOString(),
	publisher: 'RNZ World',
	publishedAt: null,
	why: { valuesShare: 1, tagsShare: 0, toneShare: 0, matchedValues: [], matchedTags: [], mood: 5 },
	isSaved
});

let requests: Array<{ url: string; method: string }>;

beforeEach(() => {
	requests = [];
	vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
		requests.push({ url: String(url), method: init?.method ?? 'GET' });
		return new Response(url.toString().includes('tag-preferences') ? '[]' : null, { status: url.toString().includes('tag-preferences') ? 200 : 204 });
	});
});

afterEach(() => vi.unstubAllGlobals());

describe('Save button in the story popup', () => {
	it('shows Save with an outline bookmark when the story is not saved', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article: story(false) });
		const button = page.getByRole('button', { name: 'Save', exact: true });
		await expect.element(button).toHaveAttribute('title', 'Save');
		await expect.element(button.element().querySelector('svg')!).toHaveAttribute('fill', 'none');
	});

	it('shows Remove from saved with a filled bookmark when the story is saved', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article: story(true) });
		const button = page.getByRole('button', { name: 'Remove from saved' });
		await expect.element(button).toHaveAttribute('title', 'Remove from saved');
		await expect.element(button.element().querySelector('svg')!).toHaveAttribute('fill', 'currentColor');
	});

	it('saves the story and says so', async () => {
		const changes: Array<{ id: number; isSaved: boolean }> = [];
		render(ArticleDetailsPopup, {
			props: { isOpen: true, article: story(false) },
			events: { savedChange: (e: CustomEvent) => changes.push(e.detail) }
		});

		await page.getByRole('button', { name: 'Save', exact: true }).click();

		await expect.poll(() => changes).toEqual([{ id: 42, isSaved: true }]);
		expect(requests).toContainEqual(expect.objectContaining({ method: 'PUT', url: expect.stringContaining('/api/articles/42/saved') }));
	});

	it('un-saves a saved story', async () => {
		const changes: Array<{ id: number; isSaved: boolean }> = [];
		render(ArticleDetailsPopup, {
			props: { isOpen: true, article: story(true) },
			events: { savedChange: (e: CustomEvent) => changes.push(e.detail) }
		});

		await page.getByRole('button', { name: 'Remove from saved' }).click();

		await expect.poll(() => changes).toEqual([{ id: 42, isSaved: false }]);
		expect(requests).toContainEqual(expect.objectContaining({ method: 'DELETE', url: expect.stringContaining('/api/articles/42/saved') }));
	});
});
