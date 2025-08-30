import { redirect, type LoadEvent } from '@sveltejs/kit';
import { auth } from '$lib/stores/auth';
import { get } from 'svelte/store';
import '$lib/sentry';

export const load = (event: LoadEvent) => {
    // Skip auth check for login page
    if (event.url.pathname === '/login') {
        return {};
    }

    const authState = get(auth);
    
    // If we're not authenticated and not on the login page, redirect to login
    if (!authState.isAuthenticated && !event.url.pathname.startsWith('/login')) {
        throw redirect(307, '/login');
    }

    return {};
}; 