import { redirect } from '@sveltejs/kit';
import { get } from 'svelte/store';
import { auth } from '$lib/stores/auth';

export const ssr = false;

export const load = () => {
    const state = get(auth);
    if (!state.isAuthenticated) {
        throw redirect(307, '/login');
    }
    if (!state.user?.isAdmin) {
        throw redirect(307, '/');
    }
    return {};
};
