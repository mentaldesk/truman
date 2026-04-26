<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import ArticleDetailsPopup from '$lib/components/ArticleDetailsPopup.svelte';
    import { mood } from '$lib/stores/mood';
    import { onMount, onDestroy } from 'svelte';
    import { API_URL } from '$lib/config';
    import { valuesStore } from '$lib/stores/values';
    import { selectedPresenter } from '$lib/stores/presenter';
    import { get } from 'svelte/store';
    import { auth } from '$lib/stores/auth';
    import { profileStore } from '$lib/stores/profile';
    import { tagPreferences } from '$lib/stores/tagPreferences';

    type RelevantArticle = {
        id: number;
        link: string;
        title: string;
        tldr: string;
        content: string;
        sentiment: number;
        tags: string[];
        relevanceScore: number;
        createdAt: string;
    };

    let articles: RelevantArticle[] = [];
    let loading = true;
    let error: string | null = null;
    let moodUnsubscribe: () => void;
    let presenterUnsubscribe: () => void;
    let tagPreferencesUnsubscribe: () => void;
    let debounceTimeout: ReturnType<typeof setTimeout> | null = null;
    let saveDebounce: ReturnType<typeof setTimeout> | null = null;
    let moodUnsub: () => void;
    let valuesUnsub: () => void;
    
    // Popup state
    let selectedArticle: RelevantArticle | null = null;
    let isPopupOpen = false;

    function handleSourcesClick() {
        console.log('Sources clicked');
        // TODO: Implement sources dialog
    }
    
    function handleRulesClick() {
        console.log('Rules clicked');
        // TODO: Implement rules dialog
    }
    
    function openArticleDetails(article: RelevantArticle) {
        selectedArticle = article;
        isPopupOpen = true;
    }
    
    function closePopup() {
        isPopupOpen = false;
        selectedArticle = null;
    }

    async function fetchArticles() {
        loading = true;
        error = null;
        let presenter: string = '';

        const unsubscribePresenter = selectedPresenter.subscribe((value: string) => { presenter = value; });

        unsubscribePresenter();
        
        try {
            // Get the current auth token directly from the store
            const authState = get(auth);
            const authToken = authState.token;
            
            const headers: Record<string, string> = { 'Content-Type': 'application/json' };
            if (authToken) {
                headers['Authorization'] = `Bearer ${authToken}`;
            }
            
            const res = await fetch(`${API_URL}/api/articles/relevant`, {
                method: 'POST',
                headers,
                body: JSON.stringify({ presenter })
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

    function debounceFetchArticles() {
        if (debounceTimeout) clearTimeout(debounceTimeout);
        debounceTimeout = setTimeout(() => {
            fetchArticles();
        }, 300);
    }

    onMount(() => {
        debounceFetchArticles();
        moodUnsubscribe = mood.subscribe(() => {
            debounceFetchArticles();
        });
        presenterUnsubscribe = selectedPresenter.subscribe(() => {
            debounceFetchArticles();
        });
        tagPreferencesUnsubscribe = tagPreferences.subscribe(() => {
            debounceFetchArticles();
        });
    });

    onDestroy(() => {
        if (moodUnsubscribe) moodUnsubscribe();
        if (presenterUnsubscribe) presenterUnsubscribe();
        if (tagPreferencesUnsubscribe) tagPreferencesUnsubscribe();
        if (debounceTimeout) clearTimeout(debounceTimeout);
    });
</script>

<div class="min-h-screen">
    <div class="sticky top-0 z-20 shadow-sm">
        <Header
            on:sourcesClick={handleSourcesClick}
            on:rulesClick={handleRulesClick}
        />
    </div>
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
                {#each articles as article (article.id)}
                    <div class="flex bg-white rounded-lg shadow p-4 gap-4 items-start hover:shadow-md transition-shadow cursor-pointer" on:click={() => openArticleDetails(article)}>
                        <div class="hidden sm:flex w-28 h-28 bg-gray-200 rounded flex-shrink-0 items-center justify-center">
                            <span class="text-gray-400 text-3xl">📰</span>
                        </div>
                        <div class="flex-1">
                            <div class="text-xl font-semibold text-blue-700 hover:underline flex items-center gap-2">
                                {article.title}
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 3h7m0 0v7m0-7L10 14m-7 7h7a2 2 0 002-2v-7" /></svg>
                            </div>
                            <p class="text-gray-500 mt-1">{article.tldr}</p>
                        </div>
                    </div>
                {/each}
            </div>
        {/if}
    </main>
</div>

<!-- Article Details Popup -->
<ArticleDetailsPopup 
    article={selectedArticle} 
    isOpen={isPopupOpen} 
    on:close={closePopup} 
/>
