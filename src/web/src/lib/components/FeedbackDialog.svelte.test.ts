import { page } from 'vitest/browser';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { render } from 'vitest-browser-svelte';
import * as Sentry from '@sentry/sveltekit';
import FeedbackDialog from './FeedbackDialog.svelte';

/** Envelopes the SDK tried to send, captured instead of going over the wire. */
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

/** All items across every envelope, as [type, payload] pairs. */
function itemsSent(): Array<[string, Record<string, unknown>]> {
	return sent.flatMap(([, items]) =>
		items.map(
			([header, payload]): [string, Record<string, unknown>] => [
				String((header as { type?: string }).type),
				payload as Record<string, unknown>
			]
		)
	);
}

async function fillInAndSend(text: string) {
	await page
		.getByRole('textbox', { name: 'What were you doing when this happened?' })
		.fill(text);
	await page.getByRole('button', { name: 'Send report' }).click();

	await expect
		.poll(() => itemsSent().some(([type]) => type === 'feedback'), { timeout: 5000 })
		.toBe(true);

	const [, feedback] = itemsSent().find(([type]) => type === 'feedback')!;
	return {
		feedback,
		context: (feedback.contexts as { feedback?: Record<string, unknown> })?.feedback
	};
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

describe('FeedbackDialog', () => {
	it('stays shut until it is opened', async () => {
		render(FeedbackDialog);

		expect(document.querySelector('dialog')?.open).toBeFalsy();
	});

	it('sends what the user typed', async () => {
		render(FeedbackDialog, { open: true });

		const { context } = await fillInAndSend('the dropdown is empty');

		expect(context?.message).toBe('the dropdown is empty');
	});

	it('links the report to the error the user just hit', async () => {
		const errorEventId = Sentry.captureException(new Error('the thing that broke'));

		render(FeedbackDialog, { open: true });

		const { context } = await fillInAndSend('the dropdown is empty');

		// Without associatedEventId the report arrives as a free-floating comment
		// instead of landing on the issue the user was looking at.
		expect(context?.associated_event_id).toBe(errorEventId);
	});

	it('confirms the report was sent', async () => {
		render(FeedbackDialog, { open: true });

		await fillInAndSend('it went blank');

		await expect
			.element(page.getByText('Thanks — that will help us fix it.'))
			.toBeInTheDocument();
	});
});
