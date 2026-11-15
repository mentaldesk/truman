export type HiddenCounts = {
    belowMood: number;
    bannedTag: number;
    mood: number;
};

export function noneShown(hidden: HiddenCounts): string {
    const recent = hidden.belowMood + hidden.bannedTag;
    if (recent === 0) return "Truman hasn't added any stories in the last two days.";
    const criteria = `at or above your mood (${hidden.mood}) without a banned tag`;
    return recent === 1
        ? `The one recent story isn't ${criteria}.`
        : `None of the ${recent} recent stories are ${criteria}.`;
}
