<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    
    export let article: {
        id: number;
        link: string;
        title: string;
        tldr: string;
        content: string;
        sentiment: number;
        tags: string[];
        relevanceScore: number;
        createdAt: string;
    } | null = null;
    
    export let isOpen = false;
    
    const dispatch = createEventDispatcher();
    
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
                    <h2 class="text-xl font-semibold text-gray-900 truncate">
                        {article.title}
                    </h2>
                    <button
                        on:click={openOriginalArticle}
                        class="flex-shrink-0 p-2 text-gray-500 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                        title="Open original article in new window"
                    >
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14 3h7m0 0v7m0-7L10 14m-7 7h7a2 2 0 002-2v-7" />
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
                    <div class="flex items-center gap-4 text-sm text-gray-600 mb-3">
                        <span class="flex items-center gap-1">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                            </svg>
                            Sentiment: {article.sentiment}/10
                        </span>
                        <span class="flex items-center gap-1">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                            </svg>
                            Relevance: {article.relevanceScore.toFixed(2)}
                        </span>
                    </div>
                    
                    {#if article.tags && article.tags.length > 0}
                        <div class="flex flex-wrap gap-2">
                            {#each article.tags as tag}
                                <span class="px-2 py-1 bg-blue-100 text-blue-800 text-xs rounded-full">
                                    {tag}
                                </span>
                            {/each}
                        </div>
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