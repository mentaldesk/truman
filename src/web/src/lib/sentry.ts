import { API_URL, SENTRY_DSN, ENVIRONMENT, SENTRY_TRACES_SAMPLE_RATE } from '$lib/config';
import * as Sentry from '@sentry/svelte';
import { browser } from '$app/environment';

// Only initialize Sentry in the browser
if (browser) {
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
    
    // Capture errors from Svelte
    beforeSend(event) {
      // Log events in development for debugging
      if (import.meta.env.DEV) {
        console.log('Sentry event (dev mode):', event);
      }
      return event;
    },
  });
}

export { Sentry }; 