const thousands = new Intl.NumberFormat('en-GB');

export function formatWordCount(count: number | null | undefined): string | null {
	if (count == null) return null;
	return count === 1 ? '1 word' : `${thousands.format(count)} words`;
}
