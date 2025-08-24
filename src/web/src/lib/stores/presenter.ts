import { writable } from 'svelte/store';

export const presenterOptions = [
    'Default',
    'Jimmy',
    'Ricky'
] as const;

export type PresenterOption = typeof presenterOptions[number];

// Store the selected presenter (defaults to 'Default')
export const selectedPresenter = writable<PresenterOption>('Default'); 