import { writable, get } from 'svelte/store';
import { mood } from './mood';
import { valuesStore } from './values';
import { auth } from './auth';
import { loadUserProfile, type UserProfileDto } from '../profile';

interface ProfileState {
    isSaving: boolean;
    error: string | null;
}

function createProfileStore() {
    const { subscribe, set, update } = writable<ProfileState>({
        isSaving: false,
        error: null
    });

    let saveDebounce: ReturnType<typeof setTimeout> | null = null;


    async function loadProfile() {
        set({ isSaving: false, error: null });
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
        loadProfile,
        // For cleanup if needed
        destroy() {
            if (saveDebounce) clearTimeout(saveDebounce);
        }
    };
}

export const profileStore = createProfileStore(); 