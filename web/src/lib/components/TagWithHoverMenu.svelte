<script lang="ts">
    import { createEventDispatcher } from 'svelte';
    
    export let tag: string;
    export let isFavorite: boolean = false;
    export let isBanned: boolean = false;
    
    const dispatch = createEventDispatcher<{
        favorite: { tag: string };
        ban: { tag: string };
        remove: { tag: string };
    }>();
    
    let showMenu = false;
    
    function handleFavorite() {
        dispatch('favorite', { tag });
        showMenu = false;
    }
    
    function handleBan() {
        dispatch('ban', { tag });
        showMenu = false;
    }
    
    function handleRemove() {
        dispatch('remove', { tag });
        showMenu = false;
    }
    
    function getTagClasses() {
        let baseClasses = 'px-2 py-1 text-xs rounded-full transition-all duration-200 cursor-pointer';
        
        if (isBanned) {
            return `${baseClasses} bg-red-100 text-red-800 hover:bg-red-200`;
        } else if (isFavorite) {
            return `${baseClasses} bg-green-100 text-green-800 hover:bg-green-200`;
        } else {
            return `${baseClasses} bg-blue-100 text-blue-800 hover:bg-blue-200`;
        }
    }
</script>

<style>
    .tag-container {
        position: relative;
        display: inline-block;
    }
    
    .hover-menu {
        position: absolute;
        top: calc(100% - 8px); /* Overlap by 8px */
        left: 0;
        z-index: 10;
        /* No margin needed since we're overlapping */
    }
    
    /* Visual connector between tag and menu */
    .hover-menu::before {
        content: '';
        position: absolute;
        top: -4px;
        left: 50%;
        transform: translateX(-50%);
        width: 0;
        height: 0;
        border-left: 4px solid transparent;
        border-right: 4px solid transparent;
        border-bottom: 4px solid #e5e7eb; /* border-gray-200 */
    }
    
    .hover-menu::after {
        content: '';
        position: absolute;
        top: -3px;
        left: 50%;
        transform: translateX(-50%);
        width: 0;
        height: 0;
        border-left: 4px solid transparent;
        border-right: 4px solid transparent;
        border-bottom: 4px solid white;
    }
</style>

<div 
    class="tag-container"
    on:mouseenter={() => showMenu = true}
    on:mouseleave={() => {
        // Small delay to prevent menu from disappearing when moving to menu
        setTimeout(() => {
            if (!showMenu) return; // Menu was already closed
            showMenu = false;
        }, 100);
    }}
>
    <span class={getTagClasses()}>
        {tag}
        {#if isFavorite}
            <span class="ml-1">❤️</span>
        {/if}
        {#if isBanned}
            <span class="ml-1">🚫</span>
        {/if}
    </span>
    
    {#if showMenu}
        <div class="hover-menu">
            <div 
                class="bg-white border border-gray-200 rounded-lg shadow-lg p-2 min-w-[120px]"
                on:mouseenter={() => showMenu = true}
                on:mouseleave={() => showMenu = false}
            >
                {#if !isFavorite && !isBanned}
                    <!-- Neutral tag - show favorite and ban options -->
                    <button
                        on:click={handleFavorite}
                        class="w-full text-left px-3 py-2 text-sm text-gray-700 hover:bg-green-50 hover:text-green-700 rounded flex items-center gap-2"
                    >
                        <span>❤️</span>
                        <span>Favorite</span>
                    </button>
                    <button
                        on:click={handleBan}
                        class="w-full text-left px-3 py-2 text-sm text-gray-700 hover:bg-red-50 hover:text-red-700 rounded flex items-center gap-2"
                    >
                        <span>🚫</span>
                        <span>Ban</span>
                    </button>
                {:else if isFavorite}
                    <!-- Favorite tag - show remove option -->
                    <button
                        on:click={handleRemove}
                        class="w-full text-left px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded flex items-center gap-2"
                    >
                        <span>❌</span>
                        <span>Remove</span>
                    </button>
                {:else if isBanned}
                    <!-- Banned tag - show remove option -->
                    <button
                        on:click={handleRemove}
                        class="w-full text-left px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 rounded flex items-center gap-2"
                    >
                        <span>❌</span>
                        <span>Remove</span>
                    </button>
                {/if}
            </div>
        </div>
    {/if}
</div>
