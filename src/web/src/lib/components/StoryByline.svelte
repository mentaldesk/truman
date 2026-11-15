<script lang="ts">
    import { formatStoryAge } from '../storyAge';
    import { formatWordCount } from '../wordCount';

    export let publisher: string | null = null;
    export let publishedAt: string | null = null;
    export let wordCount: number | null = null;
    export let link: string | null = null;

    $: details = [formatStoryAge(publishedAt), formatWordCount(wordCount)].filter(Boolean);
    $: linkText = publisher ?? 'Original article';
</script>

{#if link}
    <p class="text-sm text-gray-500">
        <a href={link} target="_blank" rel="noopener noreferrer" class="text-blue-700 hover:underline">{linkText} ↗</a>{#each details as detail}{` · ${detail}`}{/each}
    </p>
{:else if publisher || details.length}
    <p class="text-sm text-gray-500">{[publisher, ...details].filter(Boolean).join(' · ')}</p>
{/if}
