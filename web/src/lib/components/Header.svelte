<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    import { mood } from '$lib/stores/mood';
    
    const dispatch = createEventDispatcher<{
        sourcesClick: void;
        rulesClick: void;
        profileClick: void;
    }>();
    
    function handleMoodChange(event: Event) {
        const value = parseInt((event.target as HTMLInputElement).value);
        mood.set(value);
    }
</script>

<header class="p-4 border-b border-gray-200">
    <div class="max-w-7xl mx-auto flex items-center justify-between">
        <h1 class="text-2xl font-bold tracking-tight">TRUMAN.NEWS</h1>
        
        <div class="flex items-center space-x-6">
            <!-- Mood Slider -->
            <div class="flex items-center space-x-2">
                <span class="text-xl" aria-hidden="true">🙁</span>
                <input
                    type="range"
                    min="1"
                    max="10"
                    step="1"
                    bind:value={$mood}
                    on:input={handleMoodChange}
                    class="w-32 h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer"
                    aria-label="News mood filter level"
                />
                <span class="text-xl" aria-hidden="true">😊</span>
            </div>
            
            <!-- Sources Button -->
            <button
                on:click={() => dispatch('sourcesClick')}
                class="inline-flex items-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                aria-label="Configure news sources"
            >
                <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 5c7.18 0 13 5.82 13 13M6 11a7 7 0 017 7m-6 0a1 1 0 11-2 0 1 1 0 012 0z" />
                </svg>
                Sources
            </button>
            
            <!-- Rules Button -->
            <button
                on:click={() => dispatch('rulesClick')}
                class="inline-flex items-center px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                aria-label="Configure news rules"
            >
                <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
                </svg>
                Rules
            </button>
            
            <!-- Profile Button -->
            <button
                on:click={() => dispatch('profileClick')}
                class="inline-flex items-center justify-center w-10 h-10 rounded-full border border-gray-300 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
                aria-label="Open profile menu"
            >
                <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
            </button>
        </div>
    </div>
</header> 