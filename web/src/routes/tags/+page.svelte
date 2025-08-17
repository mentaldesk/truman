<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import { onMount } from 'svelte';
    import { auth } from '$lib/stores/auth';
    import { goto } from '$app/navigation';
    
    onMount(() => {
        // Redirect to login if not authenticated
        if (!$auth.isAuthenticated) {
            goto('/login');
        }
    });
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

        <!-- Main Content Placeholder -->
        <div class="bg-white rounded-lg p-6 flex-1 min-h-0">
            <div class="text-center">
                <h2 class="text-2xl font-semibold text-gray-900 mb-4">Tag Management</h2>
                <p class="text-gray-600 mb-6">
                    This is where you'll be able to manage your favorite and banned tags, 
                    organize them by priority, and customize your news experience.
                </p>
                <div class="inline-flex items-center px-4 py-2 bg-blue-100 text-blue-800 rounded-full text-sm font-medium">
                    <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
                    </svg>
                    Coming Soon
                </div>
            </div>
        </div>
    </main>
</div>
