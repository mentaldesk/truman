<script lang="ts">
    import { valueName } from '../stores/values';
    import type { RelevanceBreakdown } from '../relevance';

    export let why: RelevanceBreakdown;
    export let sentiment: number;

    $: rows = [
        { label: 'Your values', share: why.valuesShare, detail: why.matchedValues.map(valueName).join(', ') },
        { label: 'Your tags', share: why.tagsShare, detail: why.matchedTags.join(', ') },
        { label: 'Tone', share: why.toneShare, detail: `${sentiment}/10 · your mood is ${why.mood}` }
    ];
</script>

<section class="mb-4" aria-labelledby="why-its-here">
    <h3 id="why-its-here" class="text-sm font-semibold text-gray-900 mb-2">Why it's here</h3>
    <dl class="space-y-2">
        {#each rows as row}
            <div>
                <div class="flex items-center gap-3">
                    <dt class="w-24 flex-shrink-0 text-sm text-gray-700">{row.label}</dt>
                    <dd class="flex-1 h-2 bg-gray-200 rounded-full overflow-hidden">
                        <div class="h-full bg-blue-500" style="width: {Math.round(row.share * 100)}%" data-share={row.share}></div>
                    </dd>
                </div>
                {#if row.detail}
                    <dd class="text-sm text-gray-500 ml-[6.75rem]">{row.detail}</dd>
                {/if}
            </div>
        {/each}
    </dl>
</section>
