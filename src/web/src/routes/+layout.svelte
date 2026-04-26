<script lang="ts">
	import '../app.css';
	import { onMount } from 'svelte';
	import { auth } from '$lib/stores/auth';
	import { page } from '$app/stores';
	import ErrorBoundary from '$lib/components/ErrorBoundary.svelte';
	import { loadPresenterOptions } from '$lib/stores/presenter';

	let { children } = $props();

	onMount(() => {
		loadPresenterOptions();
	});
</script>

<style>
	:global(html, body) {
		background-image: url('/images/horizon.jpg');
		background-repeat: repeat-x;
		background-position: top center;
		background-size: auto 100%;
		background-attachment: fixed;
		height: 100%;
		margin: 0;
		padding: 0;
	}
</style>

{#if $auth.isLoading && $page.url.pathname !== '/login'}
	<div class="min-h-screen flex items-center justify-center">
		<div class="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-blue-500"></div>
	</div>
{:else}
	<ErrorBoundary>
		<div class="min-h-screen">
			{@render children()}
		</div>
	</ErrorBoundary>
{/if}