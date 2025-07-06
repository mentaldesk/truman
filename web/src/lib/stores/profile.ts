import { writable, get } from 'svelte/store';
import { mood } from './mood';
import { valuesStore } from './values';
import { auth } from './auth';
import { loadUserProfile, saveUserProfile, type UserProfileDto } from '../profile';

interface ProfileState {
    isSaving: boolean;
    error: string | null;
}

function createProfileStore() {
    const { subscribe, set, update } = writable<ProfileState>({
        isSaving: false,
        error: null
    });

    let isInitializing = false;
    let saveDebounce: ReturnType<typeof setTimeout> | null = null;
    let moodUnsub: () => void;
    let valuesUnsub: () => void;
    let authUnsub: () => void;

    function triggerSave() {
        if (isInitializing) return;
        if (!get(auth).isAuthenticated) return;
        if (saveDebounce) clearTimeout(saveDebounce);
        saveDebounce = setTimeout(async () => {
            update(state => ({ ...state, isSaving: true, error: null }));
            const currentMood = get(mood);
            const currentSelected = get(valuesStore).selected.map(v => v.id);
            try {
                await saveUserProfile({ mood: currentMood, selectedValues: currentSelected });
                update(state => ({ ...state, isSaving: false }));
            } catch (e) {
                update(state => ({ ...state, isSaving: false, error: e instanceof Error ? e.message : String(e) }));
            }
        }, 500);
    }

    function setupAutoSave() {
        moodUnsub = mood.subscribe(triggerSave);
        valuesUnsub = valuesStore.subscribe(triggerSave);
        authUnsub = auth.subscribe(triggerSave);
    }

    async function loadProfile() {
        isInitializing = true;
        set({ isSaving: false, error: null });
        try {
            const profile = await loadUserProfile();
            if (profile) {
                mood.set(profile.mood);
                valuesStore.setSelected(profile.selectedValues);
            }
        } catch (e) {
            update(state => ({ ...state, error: e instanceof Error ? e.message : String(e) }));
        } finally {
            isInitializing = false;
        }
    }

    setupAutoSave();

    return {
        subscribe,
        loadProfile,
        // For cleanup if needed
        destroy() {
            if (saveDebounce) clearTimeout(saveDebounce);
            if (moodUnsub) moodUnsub();
            if (valuesUnsub) valuesUnsub();
            if (authUnsub) authUnsub();
        }
    };
}

export const profileStore = createProfileStore(); 