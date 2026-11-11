import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import ArticleDetailsPopup from './ArticleDetailsPopup.svelte';

const article = {
	id: 1,
	link: 'https://example.invalid/story',
	title: 'Flood barriers held',
	tldr: '',
	content: 'Body',
	sentiment: 8,
	tags: ['nz', 'weather'],
	relevanceScore: 0.8234,
	publisher: 'RNZ World',
	publishedAt: null,
	why: {
		valuesShare: 0.6,
		tagsShare: 0.1,
		toneShare: 0.3,
		matchedValues: ['security', 'responsibility'],
		matchedTags: ['nz'],
		mood: 5
	}
};

describe('Why it\'s here in the story popup', () => {
	it('has the three rows with what matched and the tone line', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article });
		await expect.element(page.getByRole('heading', { name: "Why it's here" })).toBeInTheDocument();
		await expect.element(page.getByText('Your values')).toBeInTheDocument();
		await expect.element(page.getByText('Security, Responsibility')).toBeInTheDocument();
		await expect.element(page.getByText('Your tags')).toBeInTheDocument();
		await expect.element(page.getByText('nz', { exact: true }).first()).toBeInTheDocument();
		await expect.element(page.getByText('Tone')).toBeInTheDocument();
		await expect.element(page.getByText('8/10 · your mood is 5')).toBeInTheDocument();
	});

	it('draws each bar at its share', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article });
		await expect.element(page.getByText('Tone')).toBeInTheDocument();
		const widths = [...document.querySelectorAll<HTMLElement>('[data-share]')].map(b => b.style.width);
		expect(widths).toEqual(['60%', '10%', '30%']);
	});

	it('shows no relevance number', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article });
		await expect.element(page.getByText('Tone')).toBeInTheDocument();
		await expect.element(page.getByText(/Relevance/)).not.toBeInTheDocument();
		await expect.element(page.getByText(/0\.82/)).not.toBeInTheDocument();
	});

	it('shows a bar but no names for a row with no matches', async () => {
		render(ArticleDetailsPopup, {
			isOpen: true,
			article: { ...article, why: { ...article.why, matchedTags: [], tagsShare: 0, toneShare: 0.4 } }
		});
		await expect.element(page.getByText('Your tags')).toBeInTheDocument();
		const tagsRow = page.getByText('Your tags').element().parentElement!.parentElement!;
		expect(tagsRow.querySelectorAll('dd')).toHaveLength(1);
	});
});
