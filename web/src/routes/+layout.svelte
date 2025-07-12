<script lang="ts">
	import '../app.css';
	import { auth } from '$lib/stores/auth';
	import { page } from '$app/stores';
	
	let { children } = $props();
</script>

<style>
	.horizon-bar {
		width: 100%;
		height: 120px;
		background-image: url('/images/horizon.jpg');
		background-repeat: repeat-x;
		background-position: top center;
		background-size: auto 100%;
		background-attachment: fixed;
	}
	:global(html, body) {
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
	<div class="min-h-screen horizon-bar">
		{@render children()}
	</div>
{/if}