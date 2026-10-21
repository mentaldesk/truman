<script lang="ts">
  import { onMount } from 'svelte';

  export let fallback: string = 'Something went wrong. Please try again.';

  // Opening the dialog is the layout's job, because the same dialog is reachable from the
  // footer when nothing has broken at all.
  export let onFeedback: () => void = () => {};

  let hasError = false;

  onMount(() => {
    // Sentry's globalHandlersIntegration hooks these same events, so capturing here as well
    // would report every error twice. This listener drives the fallback UI only.
    const handleError = () => {
      hasError = true;
      onFeedback();
    };

    window.addEventListener('error', handleError);
    window.addEventListener('unhandledrejection', handleError);

    return () => {
      window.removeEventListener('error', handleError);
      window.removeEventListener('unhandledrejection', handleError);
    };
  });
</script>

{#if hasError}
  <div class="error-boundary">
    <div class="error-content">
      <h2>Oops! Something went wrong</h2>
      <p>{fallback}</p>
      <div class="error-actions">
        <button class="retry-button" on:click={() => window.location.reload()}>
          Reload Page
        </button>
        <button class="feedback-button" on:click={onFeedback}>
          Tell us what happened
        </button>
      </div>
    </div>
  </div>
{:else}
  <slot />
{/if}

<style>
  .error-boundary {
    display: flex;
    justify-content: center;
    align-items: center;
    min-height: 200px;
    padding: 1rem;
  }

  .error-content {
    text-align: center;
    max-width: 500px;
  }

  .error-actions {
    display: flex;
    gap: 0.75rem;
    justify-content: center;
    flex-wrap: wrap;
    margin-top: 1rem;
  }

  .retry-button {
    background: #3b82f6;
    color: white;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 4px;
    cursor: pointer;
  }

  .retry-button:hover {
    background: #2563eb;
  }

  .feedback-button {
    background: transparent;
    border: 1px solid currentColor;
    border-radius: 0.375rem;
    padding: 0.5rem 1rem;
    cursor: pointer;
    font: inherit;
  }
</style>
