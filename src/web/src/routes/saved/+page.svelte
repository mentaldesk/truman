<script lang="ts">
    import Header from '#lib/components/Header.svelte';
    import ArticleDetailsPopup from '#lib/components/ArticleDetailsPopup.svelte';
    import StoryCard from '#lib/components/StoryCard.svelte';
    import type { Story } from '#lib/story.js';
    import { onDestroy, onMount } from 'svelte';
    import { get } from 'svelte/store';
    import { apiFetch, SessionExpiredError } from '#lib/api.js';
    import { selectedPresenter } from '#lib/stores/presenter.js';
    import { refreshed } from '#lib/refreshed.js';

    let stories: Story[] = [];
    let loading = true;
    let error: string | null = null;
    let selected: Story | null = null;
    let presenterUnsubscribe: () => void;

    async function fetchSaved() {
        error = null;
        try {
            const presenter = encodeURIComponent(get(selectedPresenter));
            const res = await apiFetch(`/api/articles/saved?presenter=${presenter}`);
            if (!res.ok) throw new Error(`Failed to fetch saved stories (${res.status})`);
            const data = await res.json();
            stories = data.articles || [];
            if (selected) selected = refreshed(selected, stories) ?? selected;
        } catch (e) {
            if (e instanceof SessionExpiredError) return;
            error = e instanceof Error ? e.message : String(e);
        }
        loading = false;
    }

    function handleSavedChange(event: CustomEvent<{ id: number; isSaved: boolean }>) {
        const { id, isSaved } = event.detail;
        if (selected?.id === id) selected = { ...selected, isSaved };
        fetchSaved();
    }

    onMount(() => {
        presenterUnsubscribe = selectedPresenter.subscribe(() => fetchSaved());
    });

    onDestroy(() => presenterUnsubscribe?.());
</script>

<div class="min-h-screen">
    <div class="sticky top-0 z-20 shadow-sm">
        <Header />
    </div>
    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <h2 class="text-2xl font-semibold text-gray-900 mb-6">Saved</h2>
        {#if loading}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-gray-500 text-lg">Loading saved stories...</p>
            </div>
        {:else if error}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-red-500 text-lg">{error}</p>
            </div>
        {:else if stories.length === 0}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-gray-500 text-lg text-center">Nothing saved yet. Tap the bookmark on a story to keep it here.</p>
            </div>
        {:else}
            <div class="space-y-6">
                {#each stories as story (story.id)}
                    <StoryCard {story} on:click={() => (selected = story)} />
                {/each}
            </div>
        {/if}
    </main>
</div>

<ArticleDetailsPopup
    article={selected}
    isOpen={selected !== null}
    on:close={() => (selected = null)}
    on:savedChange={handleSavedChange}
/>
