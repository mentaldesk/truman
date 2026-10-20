import { sentrySvelteKit } from '@sentry/sveltekit/vite';
import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';
import tailwindcss from '@tailwindcss/vite';
import devtoolsJson from 'vite-plugin-devtools-json';
import { sveltekit } from '@sveltejs/kit/vite';
import { playwright } from '@vitest/browser-playwright';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [
		// Must precede sveltekit(). Uploads the maps built below so stack traces point at our
		// source; with no SENTRY_AUTH_TOKEN it warns and skips, which keeps fork PRs building.
		sentrySvelteKit({
			org: 'mental-desk-ltd',
			project: 'truman',
			authToken: process.env.SENTRY_AUTH_TOKEN,
			telemetry: false,
			// adapter-static produces no server, so there is nothing to auto-instrument.
			autoInstrument: false,
			adapter: 'other'
		}),
		tailwindcss(),
		sveltekit({
			preprocess: vitePreprocess(),
			adapter: adapter({
				pages: 'build',
				assets: 'build',
				fallback: 'index.html',
				precompress: false,
				strict: true
			})
		}),
		devtoolsJson()
	],
	server: {
		port: 3000,
		host: true
	},
	build: {
		// 'hidden' still emits the maps for the plugin to upload but leaves the
		// //# sourceMappingURL= comment out of the bundle. Sentry resolves them by debug ID.
		sourcemap: 'hidden'
	},
	optimizeDeps: {
		force: true
	},
	test: {
		projects: [
			{
				extends: './vite.config.ts',
				test: {
					name: 'client',
					browser: {
						enabled: true,
						provider: playwright(),
						instances: [{ browser: 'chromium' }]
					},
					include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
					exclude: ['src/lib/server/**'],
					setupFiles: ['./vitest-setup-client.ts']
				}
			},
			{
				extends: './vite.config.ts',
				test: {
					name: 'server',
					environment: 'node',
					include: ['src/**/*.{test,spec}.{js,ts}'],
					exclude: ['src/**/*.svelte.{test,spec}.{js,ts}']
				}
			}
		]
	}
});
