// Check for runtime configuration first, then fall back to build-time
export const API_URL = (typeof window !== 'undefined' && (window as any).__API_URL__) || import.meta.env.VITE_API_URL || 'http://localhost:8080'; 
export const SENTRY_DSN = (typeof window !== 'undefined' && (window as any).__SENTRY_DSN__) || import.meta.env.VITE_SENTRY_DSN || ''; 
export const ENVIRONMENT = (typeof window !== 'undefined' && (window as any).__ENVIRONMENT__) || import.meta.env.VITE_ENVIRONMENT || 'unknown'; 