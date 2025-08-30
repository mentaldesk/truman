<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import TagStratum from '$lib/components/TagStratum.svelte';
    import { onMount } from 'svelte';
    import { auth } from '$lib/stores/auth';
    import { goto } from '$app/navigation';
    import { tagPreferences } from '$lib/stores/tagPreferences';
    
    onMount(() => {
        // Redirect to login if not authenticated
        if (!$auth.isAuthenticated) {
            goto('/login');
        }
        
        // Load user's tag preferences
        tagPreferences.loadPreferences();
    });
    
    // Get tags grouped by weight, excluding banned tags (weight 0)
    $: favoriteTagsByWeight = $tagPreferences.preferences
        .filter(p => p.weight > 0)
        .reduce((acc, pref) => {
            if (!acc[pref.weight]) acc[pref.weight] = [];
            acc[pref.weight].push(pref.tag);
            return acc;
        }, {} as Record<number, string[]>);
    
    // Get banned tags (weight 0)
    $: bannedTags = $tagPreferences.preferences
        .filter(p => p.weight === 0)
        .map(p => p.tag);
    
    // Get sorted weight levels for favorites
    $: weightLevels = Object.keys(favoriteTagsByWeight)
        .map(Number)
        .sort((a, b) => b - a); // Highest priority first
    
    async function handlePromote(event: CustomEvent) {
        const { tag } = event.detail;
        try {
            await tagPreferences.promoteTag(tag);
        } catch (error) {
            console.error('Failed to promote tag:', error);
        }
    }
    
    async function handleDemote(event: CustomEvent) {
        const { tag } = event.detail;
        try {
            await tagPreferences.demoteTag(tag);
        } catch (error) {
            console.error('Failed to demote tag:', error);
        }
    }
    
    async function handleRemove(event: CustomEvent) {
        const { tag } = event.detail;
        try {
            await tagPreferences.removeTagPreference(tag);
        } catch (error) {
            console.error('Failed to remove tag:', error);
        }
    }
</script>

<div class="h-full flex flex-col">
    <div class="sticky top-0 z-20 shadow-sm">
        <Header />
    </div>
    <main class="flex-1 max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8 flex flex-col">
        <!-- Purpose section -->
        <div class="flex items-center bg-white/75 rounded-lg p-8 gap-8 mb-8 flex-shrink-0">
            <div class="flex-shrink-0 bg-gray-50 rounded-full p-4 flex items-center justify-center overflow-hidden">
                <img src="/icons/tags-large.png" alt="Tags" class="w-24 h-24 object-cover rounded-full" />
            </div>
            <div class="flex-1">
                <blockquote class="text-xl text-gray-800 italic leading-relaxed">
                    "Most people are other people. Their thoughts are someone else's opinions, their lives a mimicry, their passions a quotation."
                </blockquote>
                <p class="mt-4 text-base text-gray-500 text-right">
                    — Oscar Wilde
                </p>
            </div>
        </div>

        <!-- Main Content -->
        <div class="bg-white rounded-lg p-6 flex-1 min-h-0">
            <!-- Favorite Tags Section -->
            <div class="mb-8">
                <h2 class="text-2xl font-semibold text-gray-900 mb-6">Things you care about</h2>
                
                {#if weightLevels.length === 0}
                    <div class="text-center py-8 text-gray-400 border-2 border-dashed rounded-lg">
                        <svg class="w-12 h-12 mx-auto mb-4 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z" />
                        </svg>
                        <p class="text-lg font-medium">No favorite tags yet</p>
                        <p class="text-sm">Start by favoriting tags from articles you read</p>
                    </div>
                {:else}
                    <div class="space-y-6">
                        {#each weightLevels as weightLevel}
                            <TagStratum
                                tags={favoriteTagsByWeight[weightLevel]}
                                weight={weightLevel}
                                isHighest={weightLevel === weightLevels[0] && favoriteTagsByWeight[weightLevel].length === 1}
                                isLowest={weightLevel === 1}
                                on:promote={handlePromote}
                                on:demote={handleDemote}
                                on:remove={handleRemove}
                            />
                        {/each}
                    </div>
                {/if}
            </div>
            
            <!-- Banned Tags Section -->
            <div>
                <h2 class="text-2xl font-semibold text-gray-900 mb-6">Things you wish would disappear</h2>
                
                <TagStratum
                    tags={bannedTags}
                    weight={0}
                    isHighest={false}
                    isLowest={false}
                    on:remove={handleRemove}
                />
            </div>
        </div>
    </main>
</div>
