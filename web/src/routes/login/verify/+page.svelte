<script lang="ts">
    import { page } from '$app/stores';
    import { auth } from '$lib/stores/auth';
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    
    const API_URL = 'http://localhost:8080';
    let error: string | null = null;
    let isLoading = true;

    onMount(async () => {
        const code = $page.url.searchParams.get('code');
        
        if (!code) {
            error = 'Invalid magic link. Please request a new one.';
            isLoading = false;
            return;
        }

        try {
            const response = await fetch(`${API_URL}/auth/validate/magic?code=${encodeURIComponent(code)}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                }
            });

            if (!response.ok) {
                const data = await response.json();
                throw new Error(data.message || 'Invalid or expired magic link');
            }

            const token = await response.text();
            auth.setToken(token);
            goto('/');
        } catch (e) {
            console.error('Failed to validate magic link:', e);
            error = e instanceof Error ? e.message : 'Failed to validate magic link';
            isLoading = false;
        }
    });
</script>

<div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8 bg-white p-8 rounded-lg shadow-lg">
        <div>
            <h1 class="text-center text-4xl font-bold tracking-tight mb-2">TRUMAN.NEWS</h1>
            <h2 class="text-center text-2xl font-semibold text-gray-900 mb-8">Verifying your login</h2>
        </div>

        {#if error}
            <div class="bg-red-50 border-l-4 border-red-400 p-4">
                <div class="flex">
                    <div class="flex-shrink-0">
                        <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/>
                        </svg>
                    </div>
                    <div class="ml-3">
                        <p class="text-sm text-red-700">{error}</p>
                        <p class="text-sm text-red-700 mt-2">
                            <a href="/login" class="font-medium underline hover:text-red-800">Return to login page</a>
                        </p>
                    </div>
                </div>
            </div>
        {:else if isLoading}
            <div class="flex justify-center items-center space-x-2">
                <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
                <span class="text-gray-600">Verifying your login...</span>
            </div>
        {/if}
    </div>
</div> 