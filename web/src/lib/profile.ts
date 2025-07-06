import { API_URL } from '$lib/config';
import { get } from 'svelte/store';
import { auth } from '$lib/stores/auth';

export interface UserProfileDto {
    mood: number;
    selectedValues: string[];
}

export async function loadUserProfile(): Promise<UserProfileDto | null> {
    const token = get(auth).token;
    const res = await fetch(`${API_URL}/api/profile`, {
        method: 'GET',
        headers: token ? { 'Authorization': `Bearer ${token}` } : {},
    });
    if (res.status === 401 || res.status === 404) return null;
    if (!res.ok) throw new Error('Failed to load user profile');
    return await res.json();
}

export async function patchUserMood(mood: number): Promise<void> {
    const token = get(auth).token;
    const res = await fetch(`${API_URL}/api/profile/mood`, {
        method: 'PATCH',
        headers: {
            'Content-Type': 'application/json',
            ...(token ? { 'Authorization': `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ mood }),
    });
    if (!res.ok) throw new Error('Failed to update mood');
}

export async function patchUserValues(selectedValues: string[]): Promise<void> {
    const token = get(auth).token;
    const res = await fetch(`${API_URL}/api/profile/values`, {
        method: 'PATCH',
        headers: {
            'Content-Type': 'application/json',
            ...(token ? { 'Authorization': `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ selectedValues }),
    });
    if (!res.ok) throw new Error('Failed to update values');
} 