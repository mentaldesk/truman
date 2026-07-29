import { describe, it, expect } from 'vitest';
import { isTokenExpired, userFromToken } from './auth';

const NAME_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const EMAIL = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';
const NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const AUTH_METHOD = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod';
const ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function base64Url(value: unknown): string {
    return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Builds an unsigned JWT — jwt-decode only reads the payload, so a real signature isn't needed. */
function makeToken(payload: Record<string, unknown>): string {
    return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(payload)}.not-a-real-signature`;
}

function claims(overrides: Record<string, unknown> = {}) {
    return {
        [NAME_ID]: 'user-123',
        [EMAIL]: 'truman@example.com',
        [NAME]: 'Truman',
        [AUTH_METHOD]: 'magic_link',
        exp: Math.floor(Date.now() / 1000) + 3600,
        ...overrides
    };
}

describe('isTokenExpired', () => {
    it('is false while the token is still within its lifetime', () => {
        expect(isTokenExpired({ exp: 2000 } as never, 1_000_000)).toBe(false);
    });

    it('is true once exp has passed', () => {
        expect(isTokenExpired({ exp: 1000 } as never, 2_000_000)).toBe(true);
    });

    it('treats a token with no exp claim as unusable', () => {
        expect(isTokenExpired({} as never)).toBe(true);
    });
});

describe('userFromToken', () => {
    it('returns the user for a token that has not expired', () => {
        const user = userFromToken(makeToken(claims()));

        expect(user).toEqual({
            id: 'user-123',
            email: 'truman@example.com',
            name: 'Truman',
            provider: 'magic_link',
            isAdmin: false
        });
    });

    it('returns null for an expired token', () => {
        // The API issues 7 day tokens and rejects expired ones, so an expired token must
        // not hydrate a session — otherwise the app looks logged in but every call 401s.
        const expired = makeToken(claims({ exp: Math.floor(Date.now() / 1000) - 60 }));

        expect(userFromToken(expired)).toBeNull();
    });

    it('returns null for a token with no expiry', () => {
        const { exp, ...withoutExp } = claims();

        expect(userFromToken(makeToken(withoutExp))).toBeNull();
    });

    it('returns null for a token it cannot decode', () => {
        expect(userFromToken('this is not a jwt')).toBeNull();
    });

    it('reads the admin role from a single role claim', () => {
        const user = userFromToken(makeToken(claims({ [ROLE]: 'admin' })));

        expect(user?.isAdmin).toBe(true);
    });

    it('reads the admin role from a list of role claims', () => {
        const user = userFromToken(makeToken(claims({ [ROLE]: ['user', 'admin'] })));

        expect(user?.isAdmin).toBe(true);
    });
});
