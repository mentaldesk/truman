import { API_URL, SENTRY_DSN, ENVIRONMENT, SENTRY_TRACES_SAMPLE_RATE } from '#lib/config.js';
import * as Sentry from '@sentry/sveltekit';

Sentry.init({
	dsn: SENTRY_DSN,
	environment: ENVIRONMENT,

	// Route events through our own API rather than sentry.io directly, so ad blockers do not
	// silently drop them.
	tunnel: API_URL + '/tunnel',

	// Supplied by the API via /config.js so both ends of a trace make the same sampling decision.
	tracesSampleRate: SENTRY_TRACES_SAMPLE_RATE,
	tracePropagationTargets: ['localhost', '127.0.0.1', API_URL],

	enableLogs: true,

	integrations: [Sentry.replayIntegration()],

	// Both rates default to 0, so replayIntegration() on its own records nothing.
	replaysOnErrorSampleRate: 1.0,
	replaysSessionSampleRate: 0.1
});

// SvelteKit routes errors from load functions and rendering here rather than to window.onerror,
// which is all Sentry's global handlers see. Omit this export and they are only console.error'd.
export const handleError = Sentry.handleErrorWithSentry();
