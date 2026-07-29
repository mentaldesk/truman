import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { get } from 'svelte/store';
import { apiFetch, SessionExpiredError } from './api';
import { auth } from './stores/auth';

const { gotoMock } = vi.hoisted(() => ({ gotoMock: vi.fn() }));
vi.mock('$app/navigation', () => ({ goto: gotoMock }));

const NAME_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const AUTH_METHOD = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/authenticationmethod';

function base64Url(value: unknown): string {
    return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function makeToken(): string {
    const payload = {
        [NAME_ID]: 'user-123',
        [AUTH_METHOD]: 'magic_link',
        exp: Math.floor(Date.now() / 1000) + 3600
    };
    return `${base64Url({ alg: 'HS256', typ: 'JWT' })}.${base64Url(payload)}.not-a-real-signature`;
}

describe('apiFetch', () => {
    beforeEach(() => {
        gotoMock.mockClear();
        auth.setToken(makeToken());
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        auth.clearUser();
    });

    it('attaches the bearer token from the auth store', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response('[]', { status: 200 }));
        vi.stubGlobal('fetch', fetchMock);

        await apiFetch('/api/profile');

        const [, init] = fetchMock.mock.calls[0];
        expect(new Headers(init.headers).get('Authorization')).toBe(`Bearer ${get(auth).token}`);
    });

    it('sets a JSON content type only when there is a body', async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
        vi.stubGlobal('fetch', fetchMock);

        await apiFetch('/api/profile');
        await apiFetch('/api/profile/mood', { method: 'PATCH', body: JSON.stringify({ mood: 1 }) });

        const getInit: RequestInit = fetchMock.mock.calls[0][1];
        const patchInit: RequestInit = fetchMock.mock.calls[1][1];
        expect(new Headers(getInit.headers).has('Content-Type')).toBe(false);
        expect(new Headers(patchInit.headers).get('Content-Type')).toBe('application/json');
    });

    it('clears the session and throws SessionExpiredError on a 401', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));

        await expect(apiFetch('/api/articles/relevant', { method: 'POST' }))
            .rejects.toBeInstanceOf(SessionExpiredError);

        const state = get(auth);
        expect(state.isAuthenticated).toBe(false);
        expect(state.token).toBeNull();
        expect(state.user).toBeNull();
    });

    it('passes other error statuses through to the caller', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 500 })));

        const response = await apiFetch('/api/articles/relevant', { method: 'POST' });

        expect(response.status).toBe(500);
        expect(get(auth).isAuthenticated).toBe(true);
    });
});
