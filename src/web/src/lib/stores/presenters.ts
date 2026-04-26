import { writable } from 'svelte/store';
import { get } from 'svelte/store';
import { auth } from './auth';
import { API_URL } from '../config';

export interface Presenter {
    id: number;
    label: string;
    presenterStyle: string;
}

interface PresentersState {
    presenters: Presenter[];
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

function createPresentersStore() {
    const { subscribe, set, update } = writable<PresentersState>({
        presenters: [],
        loading: false,
        error: null
    });

    return {
        subscribe,

        async loadPresenters() {
            update(state => ({ ...state, loading: true, error: null }));
            try {
                const response = await fetch(`${API_URL}/api/admin/presenters/`, {
                    headers: authHeaders()
                });
                if (!response.ok) {
                    throw new Error(`Failed to load presenters: ${response.statusText}`);
                }
                const presenters: Presenter[] = await response.json();
                set({ presenters, loading: false, error: null });
            } catch (error) {
                console.error('Error loading presenters:', error);
                set({
                    presenters: [],
                    loading: false,
                    error: error instanceof Error ? error.message : 'Unknown error'
                });
            }
        },

        async createPresenter(label: string, presenterStyle: string) {
            const response = await fetch(`${API_URL}/api/admin/presenters/`, {
                method: 'POST',
                headers: authHeaders(),
                body: JSON.stringify({ label, presenterStyle })
            });
            if (!response.ok) {
                const text = await response.text();
                throw new Error(text || `Failed to create presenter: ${response.statusText}`);
            }
            const created: Presenter = await response.json();
            update(state => ({
                ...state,
                presenters: [...state.presenters, created].sort((a, b) => a.label.localeCompare(b.label))
            }));
            return created;
        },

        async updatePresenter(id: number, patch: { label?: string; presenterStyle?: string }) {
            const response = await fetch(`${API_URL}/api/admin/presenters/${id}`, {
                method: 'PATCH',
                headers: authHeaders(),
                body: JSON.stringify(patch)
            });
            if (!response.ok) {
                const text = await response.text();
                throw new Error(text || `Failed to update presenter: ${response.statusText}`);
            }
            const updated: Presenter = await response.json();
            update(state => ({
                ...state,
                presenters: state.presenters.map(p => p.id === id ? updated : p)
            }));
            return updated;
        },

        async deletePresenter(id: number) {
            const response = await fetch(`${API_URL}/api/admin/presenters/${id}`, {
                method: 'DELETE',
                headers: authHeaders()
            });
            if (!response.ok) {
                throw new Error(`Failed to delete presenter: ${response.statusText}`);
            }
            update(state => ({
                ...state,
                presenters: state.presenters.filter(p => p.id !== id)
            }));
        }
    };
}

export const presenters = createPresentersStore();
