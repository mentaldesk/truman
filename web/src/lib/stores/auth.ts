import { writable } from 'svelte/store';

export interface User {
    id: string;
    email: string;
    name: string;
    provider: 'facebook' | 'google' | 'microsoft' | 'magic_link';
}

interface AuthState {
    isAuthenticated: boolean;
    user: User | null;
    isLoading: boolean;
}

function createAuthStore() {
    const { subscribe, set, update } = writable<AuthState>({
        isAuthenticated: false,
        user: null,
        isLoading: false
    });

    return {
        subscribe,
        setUser: (user: User) => {
            update(state => ({
                ...state,
                isAuthenticated: true,
                user,
                isLoading: false
            }));
        },
        clearUser: () => {
            update(state => ({
                ...state,
                isAuthenticated: false,
                user: null,
                isLoading: false
            }));
        },
        setLoading: (isLoading: boolean) => {
            update(state => ({ ...state, isLoading }));
        }
    };
}

export const auth = createAuthStore(); 