import { page } from 'vitest/browser';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import * as Sentry from '@sentry/sveltekit';
import ErrorBoundary from './ErrorBoundary.svelte';

/**
 * Envelopes the SDK tried to send, captured instead of going over the wire.
 * Each entry is the parsed envelope: [header, [[itemHeader, itemPayload], ...]].
 */
let sent: Array<[Record<string, unknown>, Array<[Record<string, unknown>, unknown]>]>;

function recordingTransport() {
	return {
		send: async (envelope: unknown) => {
			sent.push(envelope as never);
			return {};
		},
		flush: async () => true
	};
}

/**
 * Fire the window error the boundary listens for. cancelable + preventDefault keeps the
 * test runner from also reporting our deliberate error as an unhandled one.
 */
function breakThePage(message: string) {
	const swallow = (e: Event) => e.preventDefault();
	window.addEventListener('error', swallow, { capture: true });
	window.dispatchEvent(
		new ErrorEvent('error', { error: new Error(message), message, cancelable: true })
	);
	window.removeEventListener('error', swallow, { capture: true });
}

beforeEach(() => {
	sent = [];
	Sentry.init({
		dsn: 'https://public@o0.ingest.sentry.io/0',
		transport: recordingTransport,
		integrations: []
	});
});

afterEach(async () => {
	await Sentry.close();
});

describe('ErrorBoundary', () => {
	it('shows a fallback instead of the page when something throws', async () => {
		render(ErrorBoundary);
		breakThePage('boom');

		await expect
			.element(page.getByText('Oops! Something went wrong'))
			.toBeInTheDocument();
	});

	it('does not put a stack trace in front of the user', async () => {
		render(ErrorBoundary);
		breakThePage('secrets in here: at Foo.bar (internal.js:1:1)');

		await expect
			.element(page.getByText('Oops! Something went wrong'))
			.toBeInTheDocument();

		// The stack trace used to be rendered in a <pre>. Sentry has a better copy of it
		// and the user has no use for it, so it must not come back.
		expect(document.querySelector('pre')).toBeNull();
		expect(document.body.textContent).not.toContain('internal.js:1:1');
	});

	it('asks for feedback as soon as the error happens', async () => {
		let opened = 0;
		render(ErrorBoundary, { onFeedback: () => (opened += 1) });

		expect(opened).toBe(0);
		breakThePage('boom');

		await expect.poll(() => opened).toBe(1);
	});

	it('offers a way back to the form if it was dismissed', async () => {
		let opened = 0;
		render(ErrorBoundary, { onFeedback: () => (opened += 1) });
		breakThePage('boom');
		await expect.poll(() => opened).toBe(1);

		await page.getByRole('button', { name: 'Tell us what happened' }).click();

		expect(opened).toBe(2);
	});
});
