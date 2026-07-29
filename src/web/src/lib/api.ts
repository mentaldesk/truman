import { get } from 'svelte/store';
import { goto } from '$app/navigation';
import { browser } from '$app/environment';
import { auth } from '$lib/stores/auth';
import { API_URL } from '$lib/config';

/**
 * Thrown when the API rejects our credentials. By the time callers see this the auth
 * store has already been cleared and a redirect to /login is under way, so there is
 * usually nothing left to do beyond not reporting it as a generic failure.
 */
export class SessionExpiredError extends Error {
    constructor() {
        super('Your session has expired. Please log in again.');
        this.name = 'SessionExpiredError';
    }
}

/**
 * Calls an authenticated API endpoint, attaching the current bearer token.
 *
 * Checking `exp` when the auth store hydrates catches the common case of returning to
 * the app after the token has lapsed, but not a token invalidated server-side, a
 * rotated signing key, clock skew, or a token that expires while the tab sits open.
 * Treating a 401 as "log out and go to /login" covers those too, instead of leaving
 * the user on a page that appears logged in but can't load anything.
 *
 * @param path API path beginning with a slash, e.g. `/api/profile`.
 */
export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
    const token = get(auth).token;

    const headers = new Headers(init.headers);
    if (init.body !== undefined && !headers.has('Content-Type')) {
        headers.set('Content-Type', 'application/json');
    }
    if (token) {
        headers.set('Authorization', `Bearer ${token}`);
    }

    const response = await fetch(`${API_URL}${path}`, { ...init, headers });

    if (response.status === 401) {
        auth.clearUser();
        if (browser) {
            await goto('/login');
        }
        throw new SessionExpiredError();
    }

    return response;
}
