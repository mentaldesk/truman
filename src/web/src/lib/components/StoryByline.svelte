<script lang="ts">
    import { formatStoryAge } from '../storyAge';

    export let publisher: string | null = null;
    export let publishedAt: string | null = null;
    export let link: string | null = null;

    $: age = formatStoryAge(publishedAt);
    $: linkText = publisher ?? 'Original article';
</script>

{#if link}
    <p class="text-sm text-gray-500">
        <a href={link} target="_blank" rel="noopener noreferrer" class="text-blue-700 hover:underline">{linkText} ↗</a>{#if age}{` · ${age}`}{/if}
    </p>
{:else if publisher || age}
    <p class="text-sm text-gray-500">{[publisher, age].filter(Boolean).join(' · ')}</p>
{/if}
