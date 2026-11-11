export function refreshed<T extends { id: number }>(item: T | null, items: T[]): T | null {
	if (!item) return null;
	return items.find((i) => i.id === item.id) ?? null;
}
