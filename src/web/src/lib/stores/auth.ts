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
    exp?: number;
}

function extractIsAdmin(decoded: JwtPayload): boolean {
    const roleClaim = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    const roles = Array.isArray(roleClaim) ? roleClaim : roleClaim ? [roleClaim] : [];
    return roles.includes('admin');
}

/**
 * jwt-decode only base64-decodes the payload — it validates neither the signature nor
 * the lifetime. The API issues tokens with a 7 day lifetime and rejects expired ones,
 * so `exp` has to be checked here or a stale token looks like a live session.
 */
export function isTokenExpired(decoded: JwtPayload, now: number = Date.now()): boolean {
    // Every token we issue carries an expiry; one without it isn't a token we can trust.
    if (typeof decoded.exp !== 'number') return true;
    return decoded.exp * 1000 <= now;
}

/**
 * Returns the user a token represents, or null when the token is unusable — either
 * undecodable or expired. A null result means the caller should treat the user as
 * logged out and discard the token.
 */
export function userFromToken(token: string): User | null {
    let decoded: JwtPayload;
    try {
        decoded = jwtDecode<JwtPayload>(token);
    } catch (error) {
        console.error('Failed to decode token:', error);
        return null;
    }

    if (isTokenExpired(decoded)) {
        console.info('Auth token has expired');
        return null;
    }

    return {
        id: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
        email: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ?? null,
        name: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ?? null,
        provider: decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod'].toLowerCase() as User['provider'],
        isAdmin: extractIsAdmin(decoded)
    };
}

function createAuthStore(): AuthStore {
    // Initialize from localStorage if we're in the browser
    const storedToken = browser ? localStorage.getItem('auth_token') : null;
    const initialUser = storedToken ? userFromToken(storedToken) : null;

    // Don't leave an unusable token behind for the next page load to trip over.
    if (browser && storedToken && !initialUser) {
        localStorage.removeItem('auth_token');
    }

    const initialState: AuthState = {
        isAuthenticated: initialUser !== null,
        user: initialUser,
        isLoading: false,
        token: initialUser ? storedToken : null
    };

    const { subscribe, set, update } = writable<AuthState>(initialState);

    function clear() {
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

    return {
        subscribe,
        set,
        update,
        setToken: (token: string) => {
            const user = userFromToken(token);
            if (!user) {
                clear();
                return;
            }

            if (browser) {
                localStorage.setItem('auth_token', token);
            }

            update(state => ({
                ...state,
                isAuthenticated: true,
                token,
                user,
                isLoading: false
            }));
        },
        clearUser: clear,
        setLoading: (isLoading: boolean) => {
            update(state => ({ ...state, isLoading }));
        }
    };
}

export const auth = createAuthStore();
