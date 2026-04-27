<script lang="ts">
    import { page } from '$app/stores';
    import { auth } from '$lib/stores/auth';
    import { onMount } from 'svelte';
    import { goto } from '$app/navigation';
    import { profileStore } from '$lib/stores/profile';
    import { API_URL, SOCIAL_AUTH_ENABLED } from '$lib/config';
    
    let error: string | null = null;
    let showEmailModal = false;
    let email = '';
    let isLoading = false;
    let showSuccessMessage = false;
    let errorTimeout: ReturnType<typeof setTimeout>;

    function setError(message: string) {
        error = message;
        // Clear any existing timeout
        if (errorTimeout) clearTimeout(errorTimeout);
        // Set new timeout to clear error after 10 seconds
        errorTimeout = setTimeout(() => {
            error = null;
        }, 10000);
    }

    onMount(() => {
        // Cleanup timeout on component unmount
        return () => {
            if (errorTimeout) clearTimeout(errorTimeout);
        };
    });

    async function handleSocialLogin(provider: 'facebook' | 'google') {
        // Store the current URL as the return URL, but specifically to the login page
        const returnUrl = `${$page.url.origin}/login`;
        // Redirect to the API's login endpoint
        window.location.href = `${API_URL}/auth/${provider}/login?returnUrl=${encodeURIComponent(returnUrl)}`;
    }

    async function handleMagicLink() {
        showEmailModal = true;
    }

    async function requestMagicLink() {
        if (!email) {
            setError('Please enter your email address');
            return;
        }
        
        isLoading = true;
        error = null;
        
        try {
            const response = await fetch(`${API_URL}/auth/start/magic?email=${encodeURIComponent(email)}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                }
            });

            if (!response.ok) {
                const data = await response.json();
                throw new Error(data.error || 'Failed to send magic link');
            }

            const data = await response.json();
            showEmailModal = false;
            showSuccessMessage = true;
            error = null;
        } catch (e) {
            console.error('Failed to request magic link:', e);
            setError(e instanceof Error ? e.message : 'Failed to send magic link');
        } finally {
            isLoading = false;
        }
    }

    // Handle the token if we're returning from a social login
    onMount(async () => {
        console.log('Page URL:', $page.url.toString());
        console.log('Search params:', Object.fromEntries($page.url.searchParams.entries()));
        
        const token = $page.url.searchParams.get('token');
        console.log('Token found:', !!token);
        
        if (token) {
            console.log('Setting token and redirecting...');
            try {
                auth.setToken(token);
                // Load user profile and set stores
                try {
                    await profileStore.loadProfile();
                } catch (e) {
                    // Ignore profile load errors, proceed to dashboard
                }
                goto('/');
            } catch (e) {
                console.error('Failed to process token:', e);
                error = 'Failed to process authentication token';
            }
        } else {
            // Check for error parameter
            const errorParam = $page.url.searchParams.get('error');
            if (errorParam) {
                error = errorParam;
            }
            console.log('No token found in URL');
        }
    });
</script>

<div class="min-h-screen flex items-center justify-center bg-gray-50 py-12 px-4 sm:px-6 lg:px-8">
    <div class="max-w-md w-full space-y-8 bg-white p-8 rounded-lg shadow-lg">
        <div>
            <h1 class="text-center text-4xl font-bold tracking-tight mb-2">TRUMAN.NEWS</h1>
            <h2 class="text-center text-2xl font-semibold text-gray-900 mb-8">Log in to your account</h2>
        </div>
        
        {#if error}
            <div class="bg-red-50 border-l-4 border-red-400 p-4 mb-4">
                <div class="flex">
                    <div class="flex-shrink-0">
                        <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/>
                        </svg>
                    </div>
                    <div class="ml-3">
                        <p class="text-sm text-red-700">{error}</p>
                    </div>
                </div>
            </div>
        {/if}

        {#if showSuccessMessage}
            <div class="bg-green-50 border-l-4 border-green-400 p-4 mb-4">
                <div class="flex">
                    <div class="flex-shrink-0">
                        <svg class="h-5 w-5 text-green-400" viewBox="0 0 20 20" fill="currentColor">
                            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                        </svg>
                    </div>
                    <div class="ml-3">
                        <p class="text-sm text-green-700">
                            We've sent a magic link to your email address. Please check your inbox and click the link to log in.
                        </p>
                    </div>
                </div>
            </div>
        {/if}
        
        <div class="space-y-4">
            <!-- Facebook Login -->
            <button
                on:click={() => SOCIAL_AUTH_ENABLED && handleSocialLogin('facebook')}
                disabled={!SOCIAL_AUTH_ENABLED}
                title={SOCIAL_AUTH_ENABLED ? undefined : 'Social login is disabled in this environment'}
                class="w-full flex items-center justify-center px-4 py-2 border border-gray-300 text-base font-medium rounded-md {SOCIAL_AUTH_ENABLED ? 'text-gray-700 bg-white hover:bg-gray-50 focus:ring-[#1877F2]' : 'text-gray-400 bg-gray-50 cursor-not-allowed'} focus:outline-none focus:ring-2 focus:ring-offset-2"
            >
                <svg class="w-5 h-5 mr-2" viewBox="0 0 24 24">
                    <path fill={SOCIAL_AUTH_ENABLED ? '#1877F2' : '#9ca3af'} d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
                </svg>
                Log in with Facebook
            </button>

            <!-- Google Login -->
            <button
                on:click={() => SOCIAL_AUTH_ENABLED && handleSocialLogin('google')}
                disabled={!SOCIAL_AUTH_ENABLED}
                title={SOCIAL_AUTH_ENABLED ? undefined : 'Social login is disabled in this environment'}
                class="w-full flex items-center justify-center px-4 py-2 border border-gray-300 text-base font-medium rounded-md {SOCIAL_AUTH_ENABLED ? 'text-gray-700 bg-white hover:bg-gray-50 focus:ring-[#4285F4]' : 'text-gray-400 bg-gray-50 cursor-not-allowed'} focus:outline-none focus:ring-2 focus:ring-offset-2"
            >
                <svg class="w-5 h-5 mr-2" viewBox="0 0 24 24">
                    <path fill={SOCIAL_AUTH_ENABLED ? '#4285F4' : '#9ca3af'} d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                    <path fill={SOCIAL_AUTH_ENABLED ? '#34A853' : '#9ca3af'} d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                    <path fill={SOCIAL_AUTH_ENABLED ? '#FBBC05' : '#9ca3af'} d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                    <path fill={SOCIAL_AUTH_ENABLED ? '#EA4335' : '#9ca3af'} d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
                </svg>
                Log in with Google
            </button>

            <!-- Magic Link Login -->
            <button
                on:click={handleMagicLink}
                class="w-full flex items-center justify-center px-4 py-2 border border-gray-300 text-base font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-gray-500"
            >
                <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/>
                </svg>
                Log in with Magic Link
            </button>
        </div>
    </div>
</div>

    <!-- Email Modal -->
    {#if showEmailModal}
        <div class="fixed inset-0 bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div class="bg-white rounded-lg p-6 max-w-sm w-full">
                <h3 class="text-lg font-medium text-gray-900 mb-4">Enter your email address</h3>
                
                {#if error}
                    <div class="mb-4 bg-red-50 border-l-4 border-red-400 p-4">
                        <div class="flex">
                            <div class="flex-shrink-0">
                                <svg class="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd"/>
                                </svg>
                            </div>
                            <div class="ml-3">
                                <p class="text-sm text-red-700">{error}</p>
                                <p class="text-sm text-red-700 mt-1">Please try again in a few minutes.</p>
                            </div>
                        </div>
                    </div>
                {/if}
                
                <form on:submit|preventDefault={requestMagicLink} class="space-y-4">
                    <div>
                        <label for="email" class="block text-sm font-medium text-gray-700">Email</label>
                        <input
                            type="email"
                            id="email"
                            bind:value={email}
                            class="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-blue-500 focus:ring-blue-500"
                            placeholder="you@example.com"
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div class="flex justify-end space-x-3">
                        <button
                            type="button"
                            on:click={() => { showEmailModal = false; error = null; }}
                            class="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-gray-500 rounded-md"
                            disabled={isLoading}
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            class="px-4 py-2 text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 rounded-md disabled:opacity-50"
                            disabled={isLoading}
                        >
                            {isLoading ? 'Sending...' : 'Send Magic Link'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    {/if} 