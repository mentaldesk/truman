import { writable } from 'svelte/store';
import { get } from 'svelte/store';
import { auth } from './auth';
import { API_URL } from '../config';

export interface TagPreference {
    tag: string;
    weight: number;
}

interface TagPreferencesState {
    preferences: TagPreference[];
    loading: boolean;
    error: string | null;
}

function createTagPreferencesStore() {
    const { subscribe, set, update } = writable<TagPreferencesState>({
        preferences: [],
        loading: false,
        error: null
    });

    return {
        subscribe,
        
        async loadPreferences() {
            update(state => ({ ...state, loading: true, error: null }));
            
            try {
                const authStore = get(auth);
                if (!authStore.isAuthenticated) {
                    set({ preferences: [], loading: false, error: null });
                    return;
                }

                const response = await fetch(`${API_URL}/api/tag-preferences/`, {
                    headers: {
                        'Authorization': `Bearer ${authStore.token}`,
                        'Content-Type': 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`Failed to load tag preferences: ${response.statusText}`);
                }

                const preferences = await response.json();
                set({ preferences, loading: false, error: null });
            } catch (error) {
                console.error('Error loading tag preferences:', error);
                set({ 
                    preferences: [], 
                    loading: false, 
                    error: error instanceof Error ? error.message : 'Unknown error' 
                });
            }
        },

        async setTagPreference(tag: string, weight: number) {
            try {
                const authStore = get(auth);
                if (!authStore.isAuthenticated) {
                    throw new Error('User not authenticated');
                }

                const response = await fetch(`${API_URL}/api/tag-preferences/`, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${authStore.token}`,
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ tag, weight })
                });

                if (!response.ok) {
                    throw new Error(`Failed to set tag preference: ${response.statusText}`);
                }

                const newPreference = await response.json();
                
                update(state => {
                    const existingIndex = state.preferences.findIndex(p => 
                        p.tag.toLowerCase() === tag.toLowerCase()
                    );
                    
                    if (existingIndex >= 0) {
                        // Update existing preference
                        const newPreferences = [...state.preferences];
                        newPreferences[existingIndex] = newPreference;
                        console.log('Updating existing preference:', newPreference);
                        return { ...state, preferences: newPreferences };
                    } else {
                        // Add new preference
                        console.log('Adding new preference:', newPreference);
                        return { 
                            ...state, 
                            preferences: [...state.preferences, newPreference] 
                        };
                    }
                });
            } catch (error) {
                console.error('Error setting tag preference:', error);
                throw error;
            }
        },

        async removeTagPreference(tag: string) {
            try {
                const authStore = get(auth);
                if (!authStore.isAuthenticated) {
                    throw new Error('User not authenticated');
                }

                const response = await fetch(`${API_URL}/api/tag-preferences/${encodeURIComponent(tag)}`, {
                    method: 'DELETE',
                    headers: {
                        'Authorization': `Bearer ${authStore.token}`,
                        'Content-Type': 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`Failed to remove tag preference: ${response.statusText}`);
                }

                update(state => {
                    console.log('Removing preference for tag:', tag);
                    return {
                        ...state,
                        preferences: state.preferences.filter(p => 
                            p.tag.toLowerCase() !== tag.toLowerCase()
                        )
                    };
                });
            } catch (error) {
                console.error('Error removing tag preference:', error);
                throw error;
            }
        },

        async favoriteTag(tag: string) {
            await this.setTagPreference(tag, 1); // Default weight of 1
        },

        async banTag(tag: string) {
            await this.setTagPreference(tag, 0); // Weight 0 = banned
        },

        getTagStatus(tag: string): { isFavorite: boolean; isBanned: boolean; weight: number } {
            const state = get({ subscribe });
            const preference = state.preferences.find(p => 
                p.tag.toLowerCase() === tag.toLowerCase()
            );
            
            if (!preference) {
                return { isFavorite: false, isBanned: false, weight: 0 };
            }
            
            return {
                isFavorite: preference.weight > 0,
                isBanned: preference.weight === 0,
                weight: preference.weight
            };
        },

        reset() {
            set({ preferences: [], loading: false, error: null });
        }
    };
}

export const tagPreferences = createTagPreferencesStore();
