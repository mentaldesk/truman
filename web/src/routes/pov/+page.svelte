<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    
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
        <p class="mt-4 text-base text-gray-600">
            Your reality is defined by the sum of all the little things you choose to pay attention to. 
            
        </p>
        <p class="mt-4 text-base text-gray-500">
            Consciously choosing <strong>the media</strong> you consume and <strong>the sentiment</strong> 
            it conveys allows you to take control of your reality and focus on the things that are important
            to you. 
        </p>
    </main>
</div> 