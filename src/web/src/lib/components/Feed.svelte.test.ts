import { page } from 'vitest/browser';
import { describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import Feed from './Feed.svelte';
import type { Story } from '#lib/story.js';

const story = (id: number, isNew: boolean): Story => ({
	id,
	link: `https://example.invalid/${id}`,
	title: `Story ${id}`,
	tldr: '',
	content: '',
	sentiment: 7,
	tags: [],
	relevanceScore: 1,
	createdAt: new Date().toISOString(),
	publisher: null,
	publishedAt: null,
	sourceWordCount: null,
	imageUrl: null,
	why: { valuesShare: 0, tagsShare: 0, toneShare: 1, matchedValues: [], matchedTags: [], mood: 5 },
	isSaved: false,
	isNew
});

const sevenForty = () => {
	const at = new Date();
	at.setHours(7, 40, 0, 0);
	return at.toISOString();
};

const titles = () =>
	Array.from(document.querySelectorAll('h2, .text-xl')).map((el) => el.textContent?.trim());

describe('Feed', () => {
	it('has no headings on a first visit', async () => {
		render(Feed, { stories: [story(1, false), story(2, false)], newSince: null });
		await expect.element(page.getByText('Story 2')).toBeInTheDocument();
		expect(document.querySelectorAll('h2')).toHaveLength(0);
	});

	it('puts new stories first under New since, then Earlier, each in the order given', async () => {
		render(Feed, { stories: [story(1, false), story(2, true), story(3, false), story(4, true)], newSince: sevenForty() });
		await expect.element(page.getByText('Story 4')).toBeInTheDocument();
		expect(titles()).toEqual(['New since 7:40 this morning', 'Story 2', 'Story 4', 'Earlier', 'Story 1', 'Story 3']);
	});

	it('says nothing new when no story has arrived since', async () => {
		render(Feed, { stories: [story(1, false)], newSince: sevenForty() });
		await expect.element(page.getByText('Story 1')).toBeInTheDocument();
		expect(titles()).toEqual(['Nothing new since 7:40 this morning', 'Earlier', 'Story 1']);
	});

	it('has no headings when every story is new', async () => {
		render(Feed, { stories: [story(1, true), story(2, true)], newSince: sevenForty() });
		await expect.element(page.getByText('Story 2')).toBeInTheDocument();
		expect(document.querySelectorAll('h2')).toHaveLength(0);
	});

	it('puts the Not shown line after the last story, below Earlier', async () => {
		render(Feed, {
			stories: [story(1, false), story(2, true)],
			newSince: sevenForty(),
			hidden: { belowMood: 4, bannedTag: 0, mood: 7 }
		});
		await expect.element(page.getByText(/Not shown/)).toBeInTheDocument();
		const all = document.body.textContent ?? '';
		expect(all.indexOf('Not shown')).toBeGreaterThan(all.indexOf('Story 1'));
		expect(all.indexOf('Story 1')).toBeGreaterThan(all.indexOf('Earlier'));
	});
});
