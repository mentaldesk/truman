const browserOrigin = typeof window !== 'undefined' ? window.location.origin : '';

// Prefer runtime config when explicitly provided, otherwise fall back to same-origin for the merged app.
export const API_URL = (typeof window !== 'undefined' && (window as any).__API_URL__) || import.meta.env.VITE_API_URL || browserOrigin;
export const SENTRY_DSN = (typeof window !== 'undefined' && (window as any).__SENTRY_DSN__) || import.meta.env.VITE_SENTRY_DSN || '';
export const ENVIRONMENT = (typeof window !== 'undefined' && (window as any).__ENVIRONMENT__) || import.meta.env.VITE_ENVIRONMENT || 'unknown';
export const SOCIAL_AUTH_ENABLED: boolean = (typeof window !== 'undefined' && (window as any).__SOCIAL_AUTH_ENABLED__ !== undefined)
    ? (window as any).__SOCIAL_AUTH_ENABLED__
    : true;

// The API hands us the same rate it uses itself, so the two ends of a distributed trace agree.
// A rate of 0 is meaningful (sample nothing), so test for undefined rather than falsiness —
// `||` would silently turn 0 into the fallback.
const runtimeTracesSampleRate = typeof window !== 'undefined'
    ? (window as any).__SENTRY_TRACES_SAMPLE_RATE__
    : undefined;
export const SENTRY_TRACES_SAMPLE_RATE: number = runtimeTracesSampleRate !== undefined
    ? Number(runtimeTracesSampleRate)
    : Number(import.meta.env.VITE_SENTRY_TRACES_SAMPLE_RATE ?? (import.meta.env.DEV ? 1 : 0.2));
