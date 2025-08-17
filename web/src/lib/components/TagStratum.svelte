<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    
    export let tags: string[] = [];
    export let weight: number;
    export let isHighest: boolean = false;
    export let isLowest: boolean = false;
    
    const dispatch = createEventDispatcher<{
        promote: { tag: string };
        demote: { tag: string };
        remove: { tag: string };
    }>();
    
    function handlePromote(tag: string) {
        dispatch('promote', { tag });
    }
    
    function handleDemote(tag: string) {
        dispatch('demote', { tag });
    }
    
    function handleRemove(tag: string) {
        dispatch('remove', { tag });
    }
</script>

<div class="bg-gray-50 rounded-lg p-4 border border-gray-200">
    <div class="flex items-center justify-between mb-3">
        <h3 class="text-lg font-medium text-gray-900">
            {#if weight === 0}
                Banned Tags
            {:else}
                Priority {weight}
            {/if}
        </h3>
        <span class="text-sm text-gray-500">
            {tags.length} tag{tags.length !== 1 ? 's' : ''}
        </span>
    </div>
    
    {#if tags.length === 0}
        <div class="text-center py-6 text-gray-400 border-2 border-dashed rounded-lg">
            {#if weight === 0}
                No banned tags yet
            {:else}
                No tags in this priority level
            {/if}
        </div>
    {:else}
        <div class="space-y-2">
            {#each tags as tag}
                <div class="flex items-center justify-between bg-white rounded-lg px-3 py-2 border border-gray-200 hover:border-gray-300 transition-colors">
                    <span class="text-gray-900 font-medium">{tag}</span>
                    <div class="flex items-center space-x-2">
                        {#if weight === 0}
                            <!-- Banned tag - only remove option -->
                            <button
                                on:click={() => handleRemove(tag)}
                                class="text-red-600 hover:text-red-800 hover:bg-red-50 p-1 rounded transition-colors"
                                title="Remove from banned tags"
                            >
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        {:else}
                            <!-- Favorite tag - promote/demote options -->
                            {#if !isHighest}
                                <button
                                    on:click={() => handlePromote(tag)}
                                    class="text-blue-600 hover:text-blue-800 hover:bg-blue-50 p-1 rounded transition-colors"
                                    title="Promote to higher priority"
                                >
                                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 15l7-7 7 7" />
                                    </svg>
                                </button>
                            {/if}
                            {#if !isLowest}
                                <button
                                    on:click={() => handleDemote(tag)}
                                    class="text-orange-600 hover:text-orange-800 hover:bg-orange-50 p-1 rounded transition-colors"
                                    title="Demote to lower priority"
                                >
                                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                                    </svg>
                                </button>
                            {/if}
                            <button
                                on:click={() => handleRemove(tag)}
                                class="text-red-600 hover:text-red-800 hover:bg-red-50 p-1 rounded transition-colors"
                                title="Remove from favorites"
                            >
                                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                                </svg>
                            </button>
                        {/if}
                    </div>
                </div>
            {/each}
        </div>
    {/if}
</div>
