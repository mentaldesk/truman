<script lang="ts">
    import StoryByline from './StoryByline.svelte';
    import MatchBadges from './MatchBadges.svelte';
    import type { Story } from '#lib/story.js';

    export let story: Story;

    let failedUrl: string | null = null;
</script>

<div class="flex bg-white rounded-lg shadow p-4 gap-4 items-start hover:shadow-md transition-shadow cursor-pointer" on:click>
    {#if story.imageUrl && story.imageUrl !== failedUrl}
        <img
            src={story.imageUrl}
            alt=""
            loading="lazy"
            referrerpolicy="no-referrer"
            class="w-16 h-16 sm:w-28 sm:h-28 object-cover rounded flex-shrink-0"
            on:error={() => (failedUrl = story.imageUrl)}
        />
    {/if}
    <div class="flex-1 min-w-0">
        <div class="text-xl font-semibold text-blue-700 hover:underline flex items-center gap-2">
            {story.title}
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 3h7m0 0v7m0-7L10 14m-7 7h7a2 2 0 002-2v-7" /></svg>
        </div>
        <p class="text-gray-500 mt-1">{story.tldr}</p>
        <div class="mt-2">
            <StoryByline publisher={story.publisher} publishedAt={story.publishedAt} wordCount={story.sourceWordCount} />
        </div>
        <MatchBadges matchedTags={story.why.matchedTags} matchedValues={story.why.matchedValues} />
    </div>
</div>
