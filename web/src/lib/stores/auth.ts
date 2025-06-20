import { writable, type Writable } from 'svelte/store';

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

interface AuthStore extends Writable<AuthState> {
    setUser: (user: User) => void;
    clearUser: () => void;
    setLoading: (isLoading: boolean) => void;
}

function createAuthStore(): AuthStore {
    const { subscribe, set, update } = writable<AuthState>({
        isAuthenticated: false,
        user: null,
        isLoading: false
    });

    return {
        subscribe,
        set,
        update,
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