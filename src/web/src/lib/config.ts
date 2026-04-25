const browserOrigin = typeof window !== 'undefined' ? window.location.origin : '';

// Prefer runtime config when explicitly provided, otherwise fall back to same-origin for the merged app.
export const API_URL = (typeof window !== 'undefined' && (window as any).__API_URL__) || import.meta.env.VITE_API_URL || browserOrigin;
export const SENTRY_DSN = (typeof window !== 'undefined' && (window as any).__SENTRY_DSN__) || import.meta.env.VITE_SENTRY_DSN || '';
export const ENVIRONMENT = (typeof window !== 'undefined' && (window as any).__ENVIRONMENT__) || import.meta.env.VITE_ENVIRONMENT || 'unknown';
