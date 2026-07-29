import { apiFetch } from '$lib/api';

export interface UserProfileDto {
    mood: number;
    selectedValues: string[];
}

export async function loadUserProfile(): Promise<UserProfileDto | null> {
    const res = await apiFetch('/api/profile', { method: 'GET' });
    // A 404 means the profile hasn't been created yet, which is a normal state.
    // A 401 is handled by apiFetch and never reaches here.
    if (res.status === 404) return null;
    if (!res.ok) throw new Error('Failed to load user profile');
    return await res.json();
}

export async function patchUserMood(mood: number): Promise<void> {
    const res = await apiFetch('/api/profile/mood', {
        method: 'PATCH',
        body: JSON.stringify({ mood }),
    });
    if (!res.ok) throw new Error('Failed to update mood');
}

export async function patchUserValues(selectedValues: string[]): Promise<void> {
    const res = await apiFetch('/api/profile/values', {
        method: 'PATCH',
        body: JSON.stringify({ selectedValues }),
    });
    if (!res.ok) throw new Error('Failed to update values');
}
