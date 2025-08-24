import { writable } from 'svelte/store';
import { mood } from './mood';
import { valuesStore } from './values';
import { loadUserProfile, type UserProfileDto } from '../profile';

interface ProfileState {
    error: string | null;
}

function createProfileStore() {
    const { subscribe, set, update } = writable<ProfileState>({
        error: null
    });

    async function loadProfile() {
        set({ error: null });
        try {
            const profile = await loadUserProfile();
            if (profile) {
                mood.set(profile.mood);
                valuesStore.setSelected(profile.selectedValues);
            }
        } catch (e) {
            update(state => ({ ...state, error: e instanceof Error ? e.message : String(e) }));
        }
    }

    return {
        subscribe,
        loadProfile
    };
}

export const profileStore = createProfileStore(); 