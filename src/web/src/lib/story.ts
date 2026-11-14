import { apiFetch } from '#lib/api.js';
import type { RelevanceBreakdown } from '#lib/relevance.js';

export type Story = {
    id: number;
    link: string;
    title: string;
    tldr: string;
    content: string;
    sentiment: number;
    tags: string[];
    relevanceScore: number;
    createdAt: string;
    publisher: string | null;
    publishedAt: string | null;
    why: RelevanceBreakdown;
    isSaved: boolean;
};

export async function setSaved(id: number, saved: boolean): Promise<void> {
    const res = await apiFetch(`/api/articles/${id}/saved`, { method: saved ? 'PUT' : 'DELETE' });
    if (!res.ok) throw new Error(`Failed to ${saved ? 'save' : 'unsave'} story (${res.status})`);
}
