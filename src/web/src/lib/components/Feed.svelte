<script lang="ts">
    import StoryCard from './StoryCard.svelte';
    import HiddenLine from './HiddenLine.svelte';
    import type { HiddenCounts } from '#lib/hidden.js';
    import type { Story } from '#lib/story.js';
    import { formatVisitTime } from '../newSince';
    import { createEventDispatcher } from 'svelte';

    export let stories: Story[];
    export let newSince: string | null = null;
    export let hidden: HiddenCounts | null = null;

    const dispatch = createEventDispatcher<{ open: Story }>();

    $: fresh = stories.filter((s) => s.isNew);
    $: earlier = stories.filter((s) => !s.isNew);
    $: split = newSince !== null && earlier.length > 0;
    $: since = newSince ? formatVisitTime(newSince) : '';
</script>

{#if split}
    <div class="space-y-6">
        <h2 class="text-sm text-gray-500">{fresh.length > 0 ? `New since ${since}` : `Nothing new since ${since}`}</h2>
        {#each fresh as story (story.id)}
            <StoryCard {story} on:click={() => dispatch('open', story)} />
        {/each}
        <h2 class="text-sm text-gray-500">Earlier</h2>
        {#each earlier as story (story.id)}
            <StoryCard {story} on:click={() => dispatch('open', story)} />
        {/each}
    </div>
{:else}
    <div class="space-y-6">
        {#each stories as story (story.id)}
            <StoryCard {story} on:click={() => dispatch('open', story)} />
        {/each}
    </div>
{/if}
{#if hidden}
    <div class="mt-6">
        <HiddenLine {hidden} />
    </div>
{/if}
