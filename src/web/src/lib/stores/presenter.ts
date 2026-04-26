import { writable } from 'svelte/store';
import { API_URL } from '../config';

export interface PresenterOption {
    id: number;
    label: string;
}

export const presenterOptions = writable<PresenterOption[]>([]);
export const selectedPresenter = writable<string>('');

export async function loadPresenterOptions() {
    try {
        const response = await fetch(`${API_URL}/api/presenters/available`);
        if (!response.ok) return;
        const opts: PresenterOption[] = await response.json();
        presenterOptions.set(opts);
        selectedPresenter.update(curr => {
            if (opts.length === 0) return '';
            if (opts.some(o => o.label === curr)) return curr;
            return opts[0].label;
        });
    } catch (error) {
        console.error('Failed to load presenter options:', error);
    }
}
