<script lang="ts">
    import { valueName } from '../stores/values';

    export let matchedTags: string[] = [];
    export let matchedValues: string[] = [];

    const maxShown = 3;

    $: badges = [
        ...matchedTags.map(tag => ({ label: tag, kind: 'tag' })),
        ...matchedValues.map(id => ({ label: valueName(id), kind: 'value' }))
    ];
    $: shown = badges.slice(0, maxShown);
    $: hidden = badges.length - shown.length;
</script>

{#if badges.length > 0}
    <ul class="mt-2 flex flex-wrap gap-1" aria-label="Why it's here">
        {#each shown as badge}
            <li class="px-2 py-0.5 text-xs rounded-full {badge.kind === 'tag' ? 'bg-green-100 text-green-800' : 'bg-purple-100 text-purple-800'}" data-kind={badge.kind}>{badge.label}</li>
        {/each}
        {#if hidden > 0}
            <li class="px-2 py-0.5 text-xs rounded-full bg-gray-100 text-gray-600">+{hidden}</li>
        {/if}
    </ul>
{/if}
