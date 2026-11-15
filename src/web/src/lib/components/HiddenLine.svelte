<script lang="ts">
    import type { HiddenCounts } from '#lib/hidden.js';

    export let hidden: HiddenCounts;
</script>

{#if hidden.belowMood > 0 || hidden.bannedTag > 0}
    <p class="text-sm text-gray-500">
        Not shown:
        {#if hidden.belowMood > 0}{hidden.belowMood} below your mood ({hidden.mood}){/if}{#if hidden.belowMood > 0 && hidden.bannedTag > 0}{' · '}{/if}{#if hidden.bannedTag > 0}{hidden.bannedTag} with <a href="/tags" class="text-blue-700 hover:underline">a banned tag</a>{/if}
    </p>
{/if}
