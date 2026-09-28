import { API_URL, SENTRY_DSN, ENVIRONMENT, SENTRY_TRACES_SAMPLE_RATE } from '$lib/config';
import * as Sentry from '@sentry/svelte';
import type { HandleClientError } from '@sveltejs/kit';

// This file runs in the browser before anything else in the app, which makes it the right
// place to bring Sentry up. It also gives us somewhere to export handleError from, which is
// the only way to see the errors SvelteKit catches for itself (see below).
Sentry.init({
  dsn: SENTRY_DSN,
  environment: ENVIRONMENT,
  tunnel: API_URL + '/tunnel',

  // Performance monitoring. Supplied by the API via /config.js so the browser and the
  // backend make the same sampling decision — a trace sampled in at one end and out at
  // the other is worse than either extreme.
  tracesSampleRate: SENTRY_TRACES_SAMPLE_RATE,

  // Distributed tracing - include your API URL
  tracePropagationTargets: [
    'localhost',
    '127.0.0.1',
    ...((window as any).__API_URL__ ? [(window as any).__API_URL__] : [])
  ],

  // Enable automatic instrumentation
  integrations: [
    Sentry.browserTracingIntegration(),
    Sentry.replayIntegration({
      maskAllText: false,
      blockAllMedia: false,
    }),
  ],

  beforeSend(event) {
    // Log events in development for debugging
    if (import.meta.env.DEV) {
      console.log('Sentry event (dev mode):', event);
    }
    return event;
  },
});

// SvelteKit catches errors thrown in load functions and during component rendering and
// routes them here instead of letting them reach window.onerror. Sentry's global handlers
// only see window.onerror, so without this hook that entire class of error — the ones that
// break a page rather than a background task — is invisible.
//
// @sentry/sveltekit ships handleErrorWithSentry for this. @sentry/svelte does not, because
// it has no SvelteKit-specific surface, so we write it out by hand.
export const handleError: HandleClientError = ({ error, event, status, message }) => {
  // Defensive: SvelteKit documents this hook as handling *unexpected* errors, and an
  // unmatched route in this app triggers a full page navigation rather than reaching here,
  // so a 404 is not expected to arrive. Guarding anyway, because reporting missing pages as
  // exceptions would be noisy and the status is part of the hook's contract.
  if (status !== 404) {
    Sentry.captureException(error, {
      extra: { status, message, routeId: event.route?.id },
    });
  }

  return { message };
};
