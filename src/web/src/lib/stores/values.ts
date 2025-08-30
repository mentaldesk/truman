import { writable } from 'svelte/store';

interface Value {
    id: string;
    name: string;
    description: string;
    category: {
        id: string;
        name: string;
        emoji: string;
    };
}

const categories = [
    { id: 'autonomy', name: 'Autonomy & Self-Agency', emoji: '✨' },
    { id: 'interpersonal', name: 'Interpersonal & Moral', emoji: '🌍' },
    { id: 'society', name: 'Society & Structure', emoji: '🏛️' },
    { id: 'achievement', name: 'Achievement & Progress', emoji: '🎯' },
    { id: 'wellbeing', name: 'Well-Being & Experience', emoji: '💚' }
];

const initialValues: Value[] = [
    // Autonomy & Self-Agency
    { id: 'freedom', name: 'Freedom', description: 'The right to live, think, and act freely', category: categories[0] },
    { id: 'independence', name: 'Independence', description: 'Relying on yourself; self-sufficiency', category: categories[0] },
    { id: 'self-respect', name: 'Self-Respect', description: 'Pride in who you are and how you act', category: categories[0] },
    { id: 'self-actualization', name: 'Self-Actualization', description: 'Becoming the best version of yourself', category: categories[0] },
    { id: 'creativity', name: 'Creativity', description: 'Expressing new ideas and original thought', category: categories[0] },
    
    // Interpersonal & Moral
    { id: 'honesty', name: 'Honesty', description: 'Telling the truth and being transparent', category: categories[1] },
    { id: 'compassion', name: 'Compassion', description: 'Caring for others and their well-being', category: categories[1] },
    { id: 'loyalty', name: 'Loyalty', description: 'Standing by people and commitments', category: categories[1] },
    { id: 'justice', name: 'Justice', description: 'Treating people fairly and upholding rights', category: categories[1] },
    { id: 'responsibility', name: 'Responsibility', description: 'Being accountable for your actions', category: categories[1] },
    
    // Society & Structure
    { id: 'security', name: 'Security', description: 'Physical, financial, and emotional safety', category: categories[2] },
    { id: 'equality', name: 'Equality', description: 'Fair treatment regardless of identity', category: categories[2] },
    { id: 'tradition', name: 'Tradition', description: 'Respecting customs and cultural continuity', category: categories[2] },
    { id: 'obedience', name: 'Obedience', description: 'Following rules and authority', category: categories[2] },
    
    // Achievement & Progress
    { id: 'success', name: 'Success', description: 'Accomplishing goals and excelling', category: categories[3] },
    { id: 'ambition', name: 'Ambition', description: 'Striving to achieve more', category: categories[3] },
    { id: 'discipline', name: 'Discipline', description: 'Controlling impulses to reach long-term goals', category: categories[3] },
    { id: 'knowledge', name: 'Knowledge', description: 'Seeking truth and understanding', category: categories[3] },
    { id: 'open-mindedness', name: 'Open-Mindedness', description: 'Being receptive to different views', category: categories[3] },
    
    // Well-Being & Experience
    { id: 'peace-of-mind', name: 'Peace of Mind', description: 'Inner calm and lack of anxiety', category: categories[4] },
    { id: 'pleasure', name: 'Pleasure', description: 'Enjoying life\'s moments', category: categories[4] },
    { id: 'connection', name: 'Connection', description: 'Feeling close to others', category: categories[4] },
    { id: 'adventure', name: 'Adventure', description: 'Seeking novelty and challenge', category: categories[4] }
];

function createValuesStore() {
    const { subscribe, set, update } = writable({
        available: initialValues,
        selected: [] as Value[]
    });

    return {
        subscribe,
        selectValue: (value: Value) => update(state => ({
            available: state.available.filter(v => v.id !== value.id),
            selected: [...state.selected, value]
        })),
        unselectValue: (value: Value) => update(state => ({
            available: [...state.available, value],
            selected: state.selected.filter(v => v.id !== value.id)
        })),
        reorderSelected: (fromIndex: number, toIndex: number) => update(state => {
            const selected = [...state.selected];
            const [removed] = selected.splice(fromIndex, 1);
            selected.splice(toIndex, 0, removed);
            return { ...state, selected };
        }),
        reset: () => set({ available: initialValues, selected: [] }),
        setSelected: (ids: string[]) => set({
            available: initialValues.filter(v => !ids.includes(v.id)),
            selected: ids.map(id => initialValues.find(v => v.id === id)).filter(Boolean) as Value[]
        })
    };
}

export const valuesStore = createValuesStore(); 