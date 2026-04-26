<script lang="ts">
    import { createEventDispatcher, onMount } from 'svelte';
    import { mood } from '$lib/stores/mood';
    import { auth } from '$lib/stores/auth';
    import { selectedPresenter, presenterOptions } from '$lib/stores/presenter';
    import { goto } from '$app/navigation';
    import { patchUserMood } from '$lib/profile';
    
    const dispatch = createEventDispatcher<{
        profileClick: void;
    }>();
    
    let menuOpen = false;
    let moodSaveDebounce: ReturnType<typeof setTimeout> | null = null;
    
    function handleMoodChange(event: Event) {
        const value = parseInt((event.target as HTMLInputElement).value);
        mood.set(value);
        if (moodSaveDebounce) clearTimeout(moodSaveDebounce);
        moodSaveDebounce = setTimeout(() => {
            patchUserMood(value).catch(() => {});
        }, 500);
    }
    
    function handleLogout() {
        auth.clearUser();
        menuOpen = false;
        goto('/login');
    }

    function handlePerspectiveClick() {
        menuOpen = false;
        goto('/pov');
    }
    
    function handleTagsClick() {
        menuOpen = false;
        goto('/tags');
    }

    function handleAdminFeedsClick() {
        menuOpen = false;
        goto('/admin/feeds');
    }

    function handleAdminPresentersClick() {
        menuOpen = false;
        goto('/admin/presenters');
    }
</script>

<div class="bg-gray-100/50 w-full border-b border-gray-200">
  <!-- Title bar -->
  <div class="px-4 py-4 flex items-center justify-between">
    <a href="/" class="flex-shrink-0">
      <h1 class="text-2xl font-bold text-gray-900">TRUMAN.NEWS</h1>
    </a>

    <!-- Desktop controls -->
    <div class="hidden sm:flex items-center space-x-6">
      <!-- Mood Slider -->
      <div class="flex items-center space-x-2">
        <span class="text-xl" aria-hidden="true">🙁</span>
        <input
          type="range"
          min="1"
          max="10"
          step="1"
          bind:value={$mood}
          on:input={handleMoodChange}
          class="w-32 h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer"
          aria-label="News mood filter level"
        />
        <span class="text-xl" aria-hidden="true">😊</span>
      </div>
      <!-- Presenter Dropdown -->
      <div class="flex items-center space-x-2">
        <label for="presenter-select-desktop" class="text-sm font-medium text-gray-700">Presenter:</label>
        <select
          id="presenter-select-desktop"
          bind:value={$selectedPresenter}
          class="block w-32 px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
          aria-label="Select news presenter style"
        >
          {#each $presenterOptions as option (option.id)}
            <option value={option.label}>{option.label}</option>
          {/each}
        </select>
      </div>
      <!-- Profile Button -->
      <div class="relative">
        <button
          on:click={() => menuOpen = !menuOpen}
          class="inline-flex items-center justify-center w-10 h-10 rounded-full border border-gray-300 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
          aria-label="Open profile menu"
          aria-expanded={menuOpen}
          aria-haspopup="true"
        >
          <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
          </svg>
        </button>
        {#if menuOpen}
          <div
            class="absolute right-0 mt-2 min-w-[12rem] max-w-sm rounded-md shadow-lg bg-white ring-1 ring-black ring-opacity-5 divide-y divide-gray-100 z-50"
            role="menu"
            aria-orientation="vertical"
          >
            <div class="py-3 px-4">
              <p class="text-sm font-medium text-gray-900 break-words">{$auth.user?.name || 'Anonymous User'}</p>
              <p class="text-xs text-gray-500 mt-1 break-words">{$auth.user?.email || 'No email'}</p>
            </div>
            <div class="py-1" role="none">
              <button on:click={handlePerspectiveClick} class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">
                <img src="/icons/telescope.svg" alt="" class="w-5 h-5 mr-2 inline-block" />Perspective
              </button>
              <button on:click={handleTagsClick} class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">
                <img src="/icons/tags.svg" alt="" class="w-5 h-5 mr-2 inline-block" />Tags
              </button>
              {#if $auth.user?.isAdmin}
                <button on:click={handleAdminFeedsClick} class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">
                  <svg class="w-5 h-5 mr-2 inline-block" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 11a9 9 0 019 9M4 4a16 16 0 0116 16M6 19a1 1 0 11-2 0 1 1 0 012 0z" />
                  </svg>Feeds
                </button>
                <button on:click={handleAdminPresentersClick} class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">
                  <svg class="w-5 h-5 mr-2 inline-block" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10a3 3 0 11-6 0 3 3 0 016 0zM6 21v-1a6 6 0 0112 0v1" />
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 6.343a8 8 0 010 11.314M19.778 4.222a11 11 0 010 15.556" />
                  </svg>Presenters
                </button>
              {/if}
              <button on:click={handleLogout} class="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-100" role="menuitem">
                <svg class="w-5 h-5 mr-2 inline-block" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>Sign out
              </button>
            </div>
          </div>
        {/if}
      </div>
    </div>

    <!-- Mobile hamburger button -->
    <button
      class="sm:hidden inline-flex items-center justify-center w-10 h-10 rounded-md border border-gray-300 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
      on:click={() => menuOpen = !menuOpen}
      aria-label={menuOpen ? 'Close menu' : 'Open menu'}
      aria-expanded={menuOpen}
    >
      {#if menuOpen}
        <!-- X icon -->
        <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      {:else}
        <!-- Hamburger icon -->
        <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
        </svg>
      {/if}
    </button>
  </div>

  <!-- Mobile expanded panel -->
  {#if menuOpen}
    <div class="sm:hidden border-t border-gray-200 bg-white px-4 py-4 space-y-4">
      <!-- User info -->
      <div class="pb-3 border-b border-gray-100">
        <p class="text-sm font-medium text-gray-900">{$auth.user?.name || 'Anonymous User'}</p>
        <p class="text-xs text-gray-500 mt-1">{$auth.user?.email || 'No email'}</p>
      </div>

      <!-- Mood Slider -->
      <div>
        <label class="block text-sm font-medium text-gray-700 mb-2">Mood</label>
        <div class="flex items-center space-x-3">
          <span class="text-xl" aria-hidden="true">🙁</span>
          <input
            type="range"
            min="1"
            max="10"
            step="1"
            bind:value={$mood}
            on:input={handleMoodChange}
            class="flex-1 h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer"
            aria-label="News mood filter level"
          />
          <span class="text-xl" aria-hidden="true">😊</span>
        </div>
      </div>

      <!-- Presenter Dropdown -->
      <div>
        <label for="presenter-select-mobile" class="block text-sm font-medium text-gray-700 mb-2">Presenter</label>
        <select
          id="presenter-select-mobile"
          bind:value={$selectedPresenter}
          class="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 text-sm"
          aria-label="Select news presenter style"
        >
          {#each $presenterOptions as option (option.id)}
            <option value={option.label}>{option.label}</option>
          {/each}
        </select>
      </div>

      <!-- Nav links -->
      <div class="border-t border-gray-100 pt-3 space-y-1">
        <button on:click={handlePerspectiveClick} class="w-full text-left px-2 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md flex items-center">
          <img src="/icons/telescope.svg" alt="" class="w-5 h-5 mr-3" />Perspective
        </button>
        <button on:click={handleTagsClick} class="w-full text-left px-2 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md flex items-center">
          <img src="/icons/tags.svg" alt="" class="w-5 h-5 mr-3" />Tags
        </button>
        {#if $auth.user?.isAdmin}
          <button on:click={handleAdminFeedsClick} class="w-full text-left px-2 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md flex items-center">
            <svg class="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 11a9 9 0 019 9M4 4a16 16 0 0116 16M6 19a1 1 0 11-2 0 1 1 0 012 0z" />
            </svg>Feeds
          </button>
          <button on:click={handleAdminPresentersClick} class="w-full text-left px-2 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md flex items-center">
            <svg class="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10a3 3 0 11-6 0 3 3 0 016 0zM6 21v-1a6 6 0 0112 0v1" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 6.343a8 8 0 010 11.314M19.778 4.222a11 11 0 010 15.556" />
            </svg>Presenters
          </button>
        {/if}
        <button on:click={handleLogout} class="w-full text-left px-2 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-md flex items-center">
          <svg class="w-5 h-5 mr-3" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
          </svg>Sign out
        </button>
      </div>
    </div>
  {/if}
</div>
