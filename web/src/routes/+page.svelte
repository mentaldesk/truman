<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import { mood } from '$lib/stores/mood';
    
    function handleSourcesClick() {
        console.log('Sources clicked');
        // TODO: Implement sources dialog
    }
    
    function handleRulesClick() {
        console.log('Rules clicked');
        // TODO: Implement rules dialog
    }
</script>

<div class="min-h-screen bg-gray-50">
    <Header
        on:sourcesClick={handleSourcesClick}
        on:rulesClick={handleRulesClick}
    />
    
    <main class="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <!-- Content Placeholder -->
        <div class="bg-white rounded-lg shadow p-6 min-h-[400px] flex items-center justify-center">
            <p class="text-gray-500 text-lg">Content Placeholder (Mood: {$mood})</p>
        </div>
    </main>
</div>
