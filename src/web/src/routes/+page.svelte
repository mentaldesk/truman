<script lang="ts">
    import Header from '#lib/components/Header.svelte';
    import ArticleDetailsPopup from '#lib/components/ArticleDetailsPopup.svelte';
    import StoryCard from '#lib/components/StoryCard.svelte';
    import type { Story } from '#lib/story.js';
    import { mood } from '#lib/stores/mood.js';
    import { onMount, onDestroy } from 'svelte';
    import { apiFetch, SessionExpiredError } from '#lib/api.js';
    import { valuesStore } from '#lib/stores/values.js';
    import { selectedPresenter } from '#lib/stores/presenter.js';
    import { profileStore } from '#lib/stores/profile.js';
    import { tagPreferences } from '#lib/stores/tagPreferences.js';
    import { refreshed } from '#lib/refreshed.js';

    let articles: Story[] = [];
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
    let selectedArticle: Story | null = null;
    let isPopupOpen = false;

    function handleSourcesClick() {
        console.log('Sources clicked');
        // TODO: Implement sources dialog
    }
    
    function handleRulesClick() {
        console.log('Rules clicked');
        // TODO: Implement rules dialog
    }
    
    function openArticleDetails(article: Story) {
        selectedArticle = article;
        isPopupOpen = true;
    }
    
    function closePopup() {
        isPopupOpen = false;
        selectedArticle = null;
    }

    function handleSavedChange(event: CustomEvent<{ id: number; isSaved: boolean }>) {
        const { id, isSaved } = event.detail;
        articles = articles.map((a) => (a.id === id ? { ...a, isSaved } : a));
        if (selectedArticle?.id === id) selectedArticle = { ...selectedArticle, isSaved };
    }

    async function fetchArticles() {
        loading = true;
        error = null;
        let presenter: string = '';

        const unsubscribePresenter = selectedPresenter.subscribe((value: string) => { presenter = value; });

        unsubscribePresenter();
        
        try {
            const res = await apiFetch('/api/articles/relevant', {
                method: 'POST',
                body: JSON.stringify({ presenter })
            });
            if (!res.ok) throw new Error(`Failed to fetch articles (${res.status})`);
            const data = await res.json();
            articles = data.articles || [];
            if (isPopupOpen) {
                selectedArticle = refreshed(selectedArticle, articles);
                if (!selectedArticle) isPopupOpen = false;
            }
        } catch (e) {
            if (e instanceof SessionExpiredError) {
                // apiFetch is redirecting to /login. Stay in the loading state so we
                // don't flash "no articles" on the way out.
                return;
            }
            error = e instanceof Error ? e.message : String(e);
        }
        loading = false;
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
                    <StoryCard story={article} on:click={() => openArticleDetails(article)} />
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
    on:savedChange={handleSavedChange}
/>
