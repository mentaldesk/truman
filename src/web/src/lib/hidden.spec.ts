import { describe, expect, it } from 'vitest';
import { noneShown } from './hidden';

describe('noneShown', () => {
    it('says how many recent stories were all hidden', () => {
        expect(noneShown({ belowMood: 20, bannedTag: 3, mood: 7 })).toBe(
            'None of the 23 recent stories are at or above your mood (7) without a banned tag.'
        );
    });

    it('reads naturally for a single story', () => {
        expect(noneShown({ belowMood: 0, bannedTag: 1, mood: 7 })).toBe(
            "The one recent story isn't at or above your mood (7) without a banned tag."
        );
    });

    it('says Truman added nothing when the window is empty', () => {
        expect(noneShown({ belowMood: 0, bannedTag: 0, mood: 7 })).toBe(
            "Truman hasn't added any stories in the last two days."
        );
    });
});
