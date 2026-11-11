<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import TagWithHoverMenu from './TagWithHoverMenu.svelte';
    import StoryByline from './StoryByline.svelte';
    import WhyItsHere from './WhyItsHere.svelte';
    import { tagPreferences } from '../stores/tagPreferences';
    import { onMount } from 'svelte';
    import { setSaved } from '../story';
    
    export let isOpen: boolean = false;
    export let article: any = null;
    
    const dispatch = createEventDispatcher<{ close: void; savedChange: { id: number; isSaved: boolean } }>();

    let saving = false;
    $: saveLabel = article?.isSaved ? 'Remove from saved' : 'Save';

    async function toggleSaved() {
        if (!article || saving) return;
        const id = article.id;
        const isSaved = !article.isSaved;
        saving = true;
        try {
            await setSaved(id, isSaved);
            dispatch('savedChange', { id, isSaved });
        } catch (e) {
            console.error(e);
        } finally {
            saving = false;
        }
    }
    
    // Declare tagStatuses variable
    let tagStatuses: Array<{tag: string; isFavorite: boolean; isBanned: boolean; weight: number}> = [];
    
    onMount(() => {
        // Load tag preferences when component mounts
        tagPreferences.loadPreferences();
    });
    
    // Subscribe to tag preferences store to trigger re-renders
    $: tagPreferencesStore = $tagPreferences;
    
    // Reactive statement to recalculate tag statuses when store changes
    $: {
        console.log('Store changed, recalculating tag statuses:', $tagPreferences);
        tagStatuses = article?.tags?.map((tag: string) => {
            const preference = $tagPreferences.preferences.find(p => 
                p.tag.toLowerCase() === tag.toLowerCase()
            );
            return {
                tag,
                isFavorite: preference ? preference.weight > 0 : false,
                isBanned: preference ? preference.weight === 0 : false,
                weight: preference?.weight || 0
            };
        }) || [];
        console.log('New tag statuses:', tagStatuses);
    }
    
    function closePopup() {
        dispatch('close');
    }
    
    function handleBackdropClick(event: MouseEvent) {
        if (event.target === event.currentTarget) {
            closePopup();
        }
    }
    
    function openOriginalArticle() {
        if (article?.link) {
            window.open(article.link, '_blank');
        }
    }
    
    function handleTagFavorite(event: CustomEvent) {
        const { tag } = event.detail;
        tagPreferences.favoriteTag(tag);
    }
    
    function handleTagBan(event: CustomEvent) {
        const { tag } = event.detail;
        tagPreferences.banTag(tag);
    }
    
    function handleTagRemove(event: CustomEvent) {
        const { tag } = event.detail;
        tagPreferences.removeTagPreference(tag);
    }
</script>

{#if isOpen && article}
    <!-- Backdrop -->
    <div 
        class="fixed inset-0 bg-gray-200/90 z-50 flex items-center justify-center p-4"
        on:click={handleBackdropClick}
    >
        <!-- Popup Content -->
        <div class="bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[90vh] overflow-hidden">
            <!-- Header -->
            <div class="flex items-center justify-between p-6 border-b border-gray-200">
                <div class="flex items-center gap-3 flex-1 min-w-0">
                    <div class="min-w-0">
                        <h2 class="text-xl font-semibold text-gray-900 truncate">
                            {article.title}
                        </h2>
                        <div class="mt-1">
                            <StoryByline publisher={article.publisher} publishedAt={article.publishedAt} link={article.link} />
                        </div>
                    </div>
                    <button
                        on:click={openOriginalArticle}
                        class="flex-shrink-0 p-2 text-gray-500 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                        title="Open original article in new window"
                    >
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 3h7m0 0v7m0-7L10 14m-7 7h7a2 2 0 002-2v-7" />
                        </svg>
                    </button>
                    <button
                        on:click={toggleSaved}
                        disabled={saving}
                        class="flex-shrink-0 p-2 text-gray-500 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                        title={saveLabel}
                        aria-label={saveLabel}
                    >
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill={article.isSaved ? 'currentColor' : 'none'} viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 5a2 2 0 012-2h10a2 2 0 012 2v16l-7-3.5L5 21V5z" />
                        </svg>
                    </button>
                </div>
                <button
                    on:click={closePopup}
                    class="flex-shrink-0 p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
                    title="Close"
                >
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </button>
            </div>
            
            <!-- Content -->
            <div class="p-6 overflow-y-auto max-h-[calc(90vh-120px)]">
                <!-- Article metadata -->
                <div class="mb-6 p-4 bg-gray-50 rounded-lg">
                    <WhyItsHere why={article.why} sentiment={article.sentiment} />

                    {#if article.tags && article.tags.length > 0}
                        <div class="flex flex-wrap gap-2">
                            {#each tagStatuses as tagStatus (`${tagStatus.tag}-${tagStatus.isFavorite}-${tagStatus.isBanned}`)}
                                <TagWithHoverMenu 
                                    tag={tagStatus.tag}
                                    isFavorite={tagStatus.isFavorite}
                                    isBanned={tagStatus.isBanned}
                                    on:favorite={handleTagFavorite}
                                    on:ban={handleTagBan}
                                    on:remove={handleTagRemove}
                                />
                            {/each}
                        </div>
                    {/if}
                    
                    <!-- Subscribe to tag preferences store to trigger re-renders -->
                    {#if $tagPreferences.loading}
                        <div class="text-sm text-gray-500 mt-2">Loading tag preferences...</div>
                    {/if}
                </div>
                
                <!-- Article content -->
                <div class="prose prose-gray max-w-none">
                    <div class="article-content text-gray-700 leading-relaxed">
                        {article.content}
                    </div>
                </div>
            </div>
        </div>
    </div>
{/if}

<style>
    .article-content {
        white-space: pre-line; /* Preserves line breaks and wraps text */
        line-height: 1.6; /* Good readability for article content */
    }
</style> 