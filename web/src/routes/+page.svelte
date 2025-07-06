<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import { mood } from '$lib/stores/mood';
    import { onMount, onDestroy } from 'svelte';
    import { API_URL } from '$lib/config';
    import { valuesStore } from '$lib/stores/values';

    type RelevantArticle = {
        id: number;
        link: string;
        title: string;
        tldr: string;
        createdAt: string;
    };

    let articles: RelevantArticle[] = [];
    let loading = true;
    let error: string | null = null;
    let moodUnsubscribe: () => void;

    function handleSourcesClick() {
        console.log('Sources clicked');
        // TODO: Implement sources dialog
    }
    
    function handleRulesClick() {
        console.log('Rules clicked');
        // TODO: Implement rules dialog
    }

    async function fetchArticles() {
        loading = true;
        error = null;
        let minimumSentiment: number = 5;
        let selectedValues: string[] = [];
        const unsubscribeMood = mood.subscribe(value => { minimumSentiment = value; });
        const unsubscribeValues = valuesStore.subscribe(state => { selectedValues = state.selected.map(v => v.id); });
        unsubscribeMood();
        unsubscribeValues();
        try {
            const res = await fetch(`${API_URL}/api/articles/relevant`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ minimumSentiment, selectedValues })
            });
            if (!res.ok) throw new Error('Failed to fetch articles');
            const data = await res.json();
            articles = data.articles || [];
        } catch (e) {
            error = e instanceof Error ? e.message : String(e);
        } finally {
            loading = false;
        }
    }

    onMount(() => {
        fetchArticles();
        moodUnsubscribe = mood.subscribe(() => {
            fetchArticles();
        });
    });

    onDestroy(() => {
        if (moodUnsubscribe) moodUnsubscribe();
    });
</script>

<div class="min-h-screen bg-gray-50">
    <Header
        on:sourcesClick={handleSourcesClick}
        on:rulesClick={handleRulesClick}
    />
    
    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        {#if loading}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-gray-500 text-lg">Loading articles...</p>
            </div>
        {:else if error}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-red-500 text-lg">{error}</p>
            </div>
        {:else if articles.length === 0}
            <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
                <p class="text-gray-500 text-lg">No relevant articles found.</p>
            </div>
        {:else}
            <div class="space-y-6">
                {#each articles as article}
                    <div class="flex bg-white rounded-lg shadow p-4 gap-4 items-start">
                        <div class="w-28 h-28 bg-gray-200 rounded flex-shrink-0 flex items-center justify-center">
                            <span class="text-gray-400 text-3xl">📰</span>
                        </div>
                        <div class="flex-1">
                            <a href={article.link} target="_blank" rel="noopener" class="text-xl font-semibold text-blue-700 hover:underline flex items-center gap-2">
                                {article.title}
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 3h7m0 0v7m0-7L10 14m-7 7h7a2 2 0 002-2v-7" /></svg>
                            </a>
                            <p class="text-gray-500 mt-1">{article.tldr}</p>
                        </div>
                    </div>
                {/each}
            </div>
        {/if}
    </main>
</div>
