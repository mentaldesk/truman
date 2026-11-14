function clock12(at: Date): string {
	const hours = at.getHours() % 12 || 12;
	return `${hours}:${String(at.getMinutes()).padStart(2, '0')}`;
}

function clock24(at: Date): string {
	return `${String(at.getHours()).padStart(2, '0')}:${String(at.getMinutes()).padStart(2, '0')}`;
}

function startOfDay(at: Date): number {
	return new Date(at.getFullYear(), at.getMonth(), at.getDate()).getTime();
}

export function formatVisitTime(since: string, now: Date = new Date()): string {
	const at = new Date(since);
	const today = startOfDay(now);
	const yesterday = startOfDay(new Date(today - 1));
	const day = startOfDay(at);
	const hour = at.getHours();

	if (day === today) {
		if (hour < 5) return `${clock12(at)} last night`;
		if (hour < 12) return `${clock12(at)} this morning`;
		if (hour < 18) return `${clock12(at)} this afternoon`;
		return `${clock12(at)} this evening`;
	}
	if (day === yesterday) {
		return hour >= 18 ? `${clock12(at)} last night` : `yesterday at ${clock24(at)}`;
	}
	return at.toLocaleDateString('en-GB', {
		day: 'numeric',
		month: 'short',
		year: at.getFullYear() === now.getFullYear() ? undefined : 'numeric'
	});
}
