import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import StoryCard from './StoryCard.svelte';
import type { Story } from '#lib/story.js';

const pixel =
	'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=';

const story = (imageUrl: string | null): Story => ({
	id: 1,
	link: 'https://example.invalid/1',
	title: 'Flood barriers held',
	tldr: 'The new barriers held.',
	content: '',
	sentiment: 7,
	tags: [],
	relevanceScore: 1,
	createdAt: new Date().toISOString(),
	publisher: 'Phys.org',
	publishedAt: null,
	sourceWordCount: null,
	imageUrl,
	why: { valuesShare: 0, tagsShare: 0, toneShare: 1, matchedValues: [], matchedTags: [], mood: 5 },
	isSaved: false
});

describe('StoryCard', () => {
	it('shows the picture, loaded lazily and sending no referer', async () => {
		render(StoryCard, { story: story(pixel) });
		const img = page.getByRole('presentation');
		await expect.element(img).toHaveAttribute('src', pixel);
		await expect.element(img).toHaveAttribute('loading', 'lazy');
		await expect.element(img).toHaveAttribute('referrerpolicy', 'no-referrer');
		await expect.element(img).toHaveClass('object-cover');
	});

	it('shows no picture box when the story has none', async () => {
		render(StoryCard, { story: story(null) });
		await expect.element(page.getByText('Flood barriers held')).toBeInTheDocument();
		expect(document.querySelector('img')).toBeNull();
		expect(document.body.textContent).not.toContain('📰');
	});

	it('drops the picture box when the picture fails to load', async () => {
		render(StoryCard, { story: story('data:image/png;base64,AAAA') });
		await expect.poll(() => document.querySelector('img')).toBeNull();
		await expect.element(page.getByText('Flood barriers held')).toBeInTheDocument();
	});
});
