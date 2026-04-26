import { writable } from 'svelte/store';
import { get } from 'svelte/store';
import { auth } from './auth';
import { API_URL } from '../config';

export interface Feed {
    id: number;
    url: string;
    name: string;
    isEnabled: boolean;
}

interface FeedsState {
    feeds: Feed[];
    loading: boolean;
    error: string | null;
}

function authHeaders(): HeadersInit {
    const authStore = get(auth);
    return {
        'Authorization': `Bearer ${authStore.token}`,
        'Content-Type': 'application/json'
    };
}

function createFeedsStore() {
    const { subscribe, set, update } = writable<FeedsState>({
        feeds: [],
        loading: false,
        error: null
    });

    return {
        subscribe,

        async loadFeeds() {
            update(state => ({ ...state, loading: true, error: null }));
            try {
                const response = await fetch(`${API_URL}/api/admin/feeds/`, {
                    headers: authHeaders()
                });
                if (!response.ok) {
                    throw new Error(`Failed to load feeds: ${response.statusText}`);
                }
                const feeds: Feed[] = await response.json();
                set({ feeds, loading: false, error: null });
            } catch (error) {
                console.error('Error loading feeds:', error);
                set({
                    feeds: [],
                    loading: false,
                    error: error instanceof Error ? error.message : 'Unknown error'
                });
            }
        },

        async createFeed(url: string, name: string, isEnabled = true) {
            const response = await fetch(`${API_URL}/api/admin/feeds/`, {
                method: 'POST',
                headers: authHeaders(),
                body: JSON.stringify({ url, name, isEnabled })
            });
            if (!response.ok) {
                const text = await response.text();
                throw new Error(text || `Failed to create feed: ${response.statusText}`);
            }
            const created: Feed = await response.json();
            update(state => ({
                ...state,
                feeds: [...state.feeds, created].sort((a, b) => a.name.localeCompare(b.name))
            }));
            return created;
        },

        async updateFeed(id: number, patch: { name?: string; isEnabled?: boolean }) {
            const response = await fetch(`${API_URL}/api/admin/feeds/${id}`, {
                method: 'PATCH',
                headers: authHeaders(),
                body: JSON.stringify(patch)
            });
            if (!response.ok) {
                const text = await response.text();
                throw new Error(text || `Failed to update feed: ${response.statusText}`);
            }
            const updated: Feed = await response.json();
            update(state => ({
                ...state,
                feeds: state.feeds.map(f => f.id === id ? updated : f)
            }));
            return updated;
        },

        async deleteFeed(id: number) {
            const response = await fetch(`${API_URL}/api/admin/feeds/${id}`, {
                method: 'DELETE',
                headers: authHeaders()
            });
            if (!response.ok) {
                throw new Error(`Failed to delete feed: ${response.statusText}`);
            }
            update(state => ({
                ...state,
                feeds: state.feeds.filter(f => f.id !== id)
            }));
        }
    };
}

export const feeds = createFeedsStore();
