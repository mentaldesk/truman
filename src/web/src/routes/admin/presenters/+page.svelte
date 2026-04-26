<script lang="ts">
    import Header from '$lib/components/Header.svelte';
    import { onMount } from 'svelte';
    import { presenters } from '$lib/stores/presenters';

    let newLabel = '';
    let newStyle = '';
    let addError: string | null = null;
    let isAdding = false;

    let editingId: number | null = null;
    let editLabel = '';
    let editStyle = '';
    let editError: string | null = null;

    onMount(() => {
        presenters.loadPresenters();
    });

    async function handleAdd() {
        addError = null;
        const label = newLabel.trim();
        const style = newStyle.trim();
        if (!label || !style) {
            addError = 'Label and style are required';
            return;
        }
        isAdding = true;
        try {
            await presenters.createPresenter(label, style);
            newLabel = '';
            newStyle = '';
        } catch (error) {
            addError = error instanceof Error ? error.message : 'Failed to add presenter';
        } finally {
            isAdding = false;
        }
    }

    function startEdit(id: number, label: string, style: string) {
        editingId = id;
        editLabel = label;
        editStyle = style;
        editError = null;
    }

    function cancelEdit() {
        editingId = null;
        editError = null;
    }

    async function saveEdit(id: number) {
        editError = null;
        const label = editLabel.trim();
        const style = editStyle.trim();
        if (!label || !style) {
            editError = 'Label and style are required';
            return;
        }
        try {
            await presenters.updatePresenter(id, { label, presenterStyle: style });
            editingId = null;
        } catch (error) {
            editError = error instanceof Error ? error.message : 'Failed to update presenter';
        }
    }

    async function handleDelete(id: number, label: string) {
        if (!confirm(`Delete presenter "${label}"? This will also delete all generated content for past articles in this style.`)) return;
        try {
            await presenters.deletePresenter(id);
        } catch (error) {
            console.error('Failed to delete presenter:', error);
        }
    }
</script>

<div class="h-full flex flex-col">
    <div class="sticky top-0 z-20 shadow-sm">
        <Header />
    </div>
    <main class="flex-1 max-w-5xl w-full mx-auto py-6 px-4 sm:px-6 lg:px-8 flex flex-col">
        <div class="bg-white rounded-lg p-6">
            <h1 class="text-2xl font-semibold text-gray-900 mb-2">Presenters</h1>
            <p class="text-sm text-gray-500 mb-6">
                Each presenter rewrites every analysed article in its own style.
                The <em>label</em> is shown to readers; the <em>style</em> is the prompt sent to the AI.
            </p>

            <form on:submit|preventDefault={handleAdd} class="grid grid-cols-1 md:grid-cols-[1fr_2fr_auto] gap-3 mb-6">
                <input
                    type="text"
                    bind:value={newLabel}
                    placeholder="Label (e.g. Jimmy)"
                    class="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    required
                />
                <input
                    type="text"
                    bind:value={newStyle}
                    placeholder="Style prompt (e.g. Jimmy Carr)"
                    class="border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    required
                />
                <button
                    type="submit"
                    disabled={isAdding}
                    class="bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white text-sm font-medium px-4 py-2 rounded-lg"
                >
                    {isAdding ? 'Adding…' : 'Add presenter'}
                </button>
            </form>

            {#if addError}
                <p class="text-sm text-red-600 mb-4">{addError}</p>
            {/if}

            {#if $presenters.loading}
                <p class="text-sm text-gray-500">Loading…</p>
            {:else if $presenters.error}
                <p class="text-sm text-red-600">Error: {$presenters.error}</p>
            {:else if $presenters.presenters.length === 0}
                <p class="text-sm text-gray-500">No presenters configured yet.</p>
            {:else}
                <table class="w-full text-sm">
                    <thead>
                        <tr class="text-left text-gray-500 border-b">
                            <th class="py-2 pr-4">Label</th>
                            <th class="py-2 pr-4">Style prompt</th>
                            <th class="py-2"></th>
                        </tr>
                    </thead>
                    <tbody>
                        {#each $presenters.presenters as presenter (presenter.id)}
                            <tr class="border-b last:border-b-0 align-top">
                                {#if editingId === presenter.id}
                                    <td class="py-3 pr-4">
                                        <input type="text" bind:value={editLabel} class="border rounded px-2 py-1 text-sm w-full" />
                                    </td>
                                    <td class="py-3 pr-4">
                                        <input type="text" bind:value={editStyle} class="border rounded px-2 py-1 text-sm w-full" />
                                        {#if editError}
                                            <p class="text-xs text-red-600 mt-1">{editError}</p>
                                        {/if}
                                    </td>
                                    <td class="py-3 text-right whitespace-nowrap">
                                        <button on:click={() => saveEdit(presenter.id)} class="text-blue-600 hover:text-blue-800 text-sm mr-3">Save</button>
                                        <button on:click={cancelEdit} class="text-gray-500 hover:text-gray-700 text-sm">Cancel</button>
                                    </td>
                                {:else}
                                    <td class="py-3 pr-4 font-medium text-gray-900">{presenter.label}</td>
                                    <td class="py-3 pr-4 text-gray-600">{presenter.presenterStyle}</td>
                                    <td class="py-3 text-right whitespace-nowrap">
                                        <button on:click={() => startEdit(presenter.id, presenter.label, presenter.presenterStyle)} class="text-blue-600 hover:text-blue-800 text-sm mr-3">Edit</button>
                                        <button on:click={() => handleDelete(presenter.id, presenter.label)} class="text-red-600 hover:text-red-800 text-sm">Delete</button>
                                    </td>
                                {/if}
                            </tr>
                        {/each}
                    </tbody>
                </table>
            {/if}
        </div>
    </main>
</div>
