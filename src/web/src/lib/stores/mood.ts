import { writable } from 'svelte/store';
import { browser } from '$app/environment';

// Initialize from localStorage if we're in the browser, default to 5 (neutral)
const initialMood = browser ? parseInt(localStorage.getItem('mood_value') ?? '5') : 5;

function createMoodStore() {
    const { subscribe, set, update } = writable<number>(initialMood);

    return {
        subscribe,
        set: (value: number) => {
            // Ensure value is between 1 and 10
            const boundedValue = Math.max(1, Math.min(10, value));
            if (browser) {
                localStorage.setItem('mood_value', boundedValue.toString());
            }
            set(boundedValue);
        },
        reset: () => {
            if (browser) {
                localStorage.removeItem('mood_value');
            }
            set(5);
        }
    };
}

export const mood = createMoodStore(); 