<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import { onMount } from 'svelte';
    import { feeds } from '$lib/stores/feeds';

    let newUrl = '';
    let newName = '';
    let addError: string | null = null;
    let isAdding = false;

    onMount(() => {
        feeds.loadFeeds();
    });

    async function handleAdd() {
        addError = null;
        const url = newUrl.trim();
        const name = newName.trim();
        if (!url || !name) {
            addError = 'URL and name are required';
            return;
        }
        isAdding = true;
        try {
            await feeds.createFeed(url, name, true);
            newUrl = '';
            newName = '';
        } catch (error) {
            addError = error instanceof Error ? error.message : 'Failed to add feed';
        } finally {
            isAdding = false;
        }
    }

    async function toggleEnabled(id: number, current: boolean) {
        try {
            await feeds.updateFeed(id, { isEnabled: !current });
        } catch (error) {
            console.error('Failed to toggle feed:', error);
        }
    }

    async function handleDelete(id: number, name: string) {
        if (!confirm(`Delete feed "${name}"?`)) return;
        try {
            await feeds.deleteFeed(id);
        } catch (error) {
            console.error('Failed to delete feed:', error);
        }
    }
</script>

<div class="h-full flex flex-col">
    <div class="sticky top-0 z-20 shadow-sm">
        <Header />
    </div>
    <main class="flex-1 max-w-5xl w-full mx-auto py-6 px-4 sm:px-6 lg:px-8 flex flex-col">
        <div class="bg-white rounded-lg p-6">
            <h1 class="text-2xl font-semibold text-gray-900 mb-6">RSS Feeds</h1>

            <form on:submit|preventDefault={handleAdd} class="grid grid-cols-1 md:grid-cols-[2fr_1fr_auto] gap-3 mb-6">
                <input
                    type="url"
                    bind:value={newUrl}
                    placeholder="https://example.com/feed.xml"
                    class="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    required
                />
                <input
                    type="text"
                    bind:value={newName}
                    placeholder="Display name"
                    class="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    required
                />
                <button
                    type="submit"
                    disabled={isAdding}
                    class="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white text-sm font-medium px-4 py-2 rounded-lg"
                >
                    {isAdding ? 'Adding…' : 'Add feed'}
                </button>
            </form>

            {#if addError}
                <p class="text-sm text-red-600 mb-4">{addError}</p>
            {/if}

            {#if $feeds.loading}
                <p class="text-sm text-gray-500">Loading…</p>
            {:else if $feeds.error}
                <p class="text-sm text-red-600">Error: {$feeds.error}</p>
            {:else if $feeds.feeds.length === 0}
                <p class="text-sm text-gray-500">No feeds configured yet.</p>
            {:else}
                <table class="w-full text-sm">
                    <thead>
                        <tr class="text-left text-gray-500 border-b">
                            <th class="py-2 pr-4">Name</th>
                            <th class="py-2 pr-4">URL</th>
                            <th class="py-2 pr-4">Enabled</th>
                            <th class="py-2"></th>
                        </tr>
                    </thead>
                    <tbody>
                        {#each $feeds.feeds as feed (feed.id)}
                            <tr class="border-b last:border-b-0">
                                <td class="py-3 pr-4 font-medium text-gray-900">{feed.name}</td>
                                <td class="py-3 pr-4 text-gray-600 break-all">
                                    <a href={feed.url} target="_blank" rel="noopener noreferrer" class="hover:underline">
                                        {feed.url}
                                    </a>
                                </td>
                                <td class="py-3 pr-4">
                                    <label class="inline-flex items-center cursor-pointer">
                                        <input
                                            type="checkbox"
                                            checked={feed.isEnabled}
                                            on:change={() => toggleEnabled(feed.id, feed.isEnabled)}
                                            class="h-4 w-4"
                                        />
                                    </label>
                                </td>
                                <td class="py-3 text-right">
                                    <button
                                        on:click={() => handleDelete(feed.id, feed.name)}
                                        class="text-red-600 hover:text-red-800 text-sm"
                                    >
                                        Delete
                                    </button>
                                </td>
                            </tr>
                        {/each}
                    </tbody>
                </table>
            {/if}
        </div>
    </main>
</div>
