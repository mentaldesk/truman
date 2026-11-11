const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export function formatStoryAge(publishedAt: string | null | undefined, now: Date = new Date()): string | null {
	if (!publishedAt) return null;
	const published = new Date(publishedAt);
	if (Number.isNaN(published.getTime())) return null;

	const elapsed = now.getTime() - published.getTime();
	if (elapsed < MINUTE) return 'Just now';
	if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)} min ago`;
	if (elapsed < DAY) {
		const hours = Math.floor(elapsed / HOUR);
		return hours === 1 ? '1 hour ago' : `${hours} hours ago`;
	}

	return published.toLocaleDateString('en-GB', {
		day: 'numeric',
		month: 'short',
		year: published.getFullYear() === now.getFullYear() ? undefined : 'numeric'
	});
}
