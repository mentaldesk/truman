import { writable, type Writable } from 'svelte/store';
import { browser } from '$app/environment';
import { jwtDecode } from 'jwt-decode';

export interface User {
    id: string;
    email: string | null;
    name: string | null;
    provider: 'facebook' | 'google' | 'magic_link';
    isAdmin: boolean;
}

interface AuthState {
    isAuthenticated: boolean;
    user: User | null;
    isLoading: boolean;
    token: string | null;
}

interface AuthStore extends Writable<AuthState> {
    setToken: (token: string) => void;
    clearUser: () => void;
    setLoading: (isLoading: boolean) => void;
}

interface JwtPayload {
    // Use the exact claim types from ASP.NET Core
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': string;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'?: string;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'?: string;
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod': string;
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string | string[];
}

function extractIsAdmin(decoded: JwtPayload): boolean {
    const roleClaim = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    const roles = Array.isArray(roleClaim) ? roleClaim : roleClaim ? [roleClaim] : [];
    return roles.includes('admin');
}

function createAuthStore(): AuthStore {
    // Initialize from localStorage if we're in the browser
    const initialToken = browser ? localStorage.getItem('auth_token') : null;
    const initialState: AuthState = {
        isAuthenticated: false,
        user: null,
        isLoading: false,
        token: initialToken
    };

    if (initialToken) {
        try {
            const decoded = jwtDecode<JwtPayload>(initialToken);
            console.log('Decoded initial token:', decoded);
            initialState.isAuthenticated = true;
            initialState.user = {
                id: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
                email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? null,
                name: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ?? null,
                provider: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod'].toLowerCase() as User['provider'],
                isAdmin: extractIsAdmin(decoded)
            };
        } catch (error) {
            console.error('Failed to decode initial token:', error);
            if (browser) {
                localStorage.removeItem('auth_token');
            }
        }
    }

    const { subscribe, set, update } = writable<AuthState>(initialState);

    return {
        subscribe,
        set,
        update,
        setToken: (token: string) => {
            try {
                console.log('Setting new token:', token);
                const decoded = jwtDecode<JwtPayload>(token);
                console.log('Decoded new token:', decoded);
                
                if (browser) {
                    localStorage.setItem('auth_token', token);
                }

                update(state => ({
                    ...state,
                    isAuthenticated: true,
                    token,
                    user: {
                        id: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
                        email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? null,
                        name: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ?? null,
                        provider: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod'].toLowerCase() as User['provider'],
                        isAdmin: extractIsAdmin(decoded)
                    },
                    isLoading: false
                }));
            } catch (error) {
                console.error('Failed to decode token:', error);
                if (browser) {
                    localStorage.removeItem('auth_token');
                }
                update(state => ({
                    ...state,
                    isAuthenticated: false,
                    token: null,
                    user: null,
                    isLoading: false
                }));
            }
        },
        clearUser: () => {
            if (browser) {
                localStorage.removeItem('auth_token');
            }
            update(state => ({
                ...state,
                isAuthenticated: false,
                token: null,
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