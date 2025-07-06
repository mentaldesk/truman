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

export async function saveUserProfile(profile: UserProfileDto): Promise<void> {
    const token = get(auth).token;
    const res = await fetch(`${API_URL}/api/profile`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            ...(token ? { 'Authorization': `Bearer ${token}` } : {})
        },
        body: JSON.stringify(profile),
    });
    if (!res.ok) throw new Error('Failed to save user profile');
} 