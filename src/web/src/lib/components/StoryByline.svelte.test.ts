import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import StoryByline from './StoryByline.svelte';
import ArticleDetailsPopup from './ArticleDetailsPopup.svelte';

const threeHoursAgo = () => new Date(Date.now() - 3 * 3_600_000).toISOString();

describe('StoryByline on a feed card', () => {
	it('shows the publisher and the age', async () => {
		render(StoryByline, { publisher: 'Phys.org', publishedAt: threeHoursAgo() });
		await expect.element(page.getByText('Phys.org · 3 hours ago')).toBeInTheDocument();
	});

	it('shows the publisher alone with no publish date', async () => {
		render(StoryByline, { publisher: 'Phys.org', publishedAt: null });
		await expect.element(page.getByText('Phys.org', { exact: true })).toBeInTheDocument();
	});

	it('shows the age alone with no known publisher', async () => {
		render(StoryByline, { publisher: null, publishedAt: threeHoursAgo() });
		await expect.element(page.getByText('3 hours ago', { exact: true })).toBeInTheDocument();
	});

	it('is not a link', async () => {
		render(StoryByline, { publisher: 'Phys.org', publishedAt: threeHoursAgo() });
		await expect.element(page.getByRole('link')).not.toBeInTheDocument();
	});
});

describe('StoryByline in the story popup', () => {
	const article = (publisher: string | null, publishedAt: string | null) => ({
		id: 1,
		link: 'https://example.invalid/story',
		title: 'A headline',
		tldr: '',
		content: 'Body',
		sentiment: 7,
		tags: [],
		relevanceScore: 0.5,
		publisher,
		publishedAt
	});

	it('links the publisher to the original in a new tab', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article: article('Phys.org', threeHoursAgo()) });
		const link = page.getByRole('link', { name: 'Phys.org ↗' });
		await expect.element(link).toHaveAttribute('href', 'https://example.invalid/story');
		await expect.element(link).toHaveAttribute('target', '_blank');
		await expect.element(page.getByText('Phys.org ↗ · 3 hours ago')).toBeInTheDocument();
	});

	it('names the link Original article with no known publisher', async () => {
		render(ArticleDetailsPopup, { isOpen: true, article: article(null, null) });
		await expect
			.element(page.getByRole('link', { name: 'Original article ↗' }))
			.toHaveAttribute('href', 'https://example.invalid/story');
	});
});
