<script lang="ts">
  import { onMount } from 'svelte';
  import { Sentry } from '$lib/sentry';

  export let fallback: string = 'Something went wrong. Please try again.';
  
  let hasError = false;
  let error: Error | null = null;

  onMount(() => {
    // Set up error handling
    const handleError = (event: ErrorEvent) => {
      hasError = true;
      error = event.error;
      
      // Capture error in Sentry
      Sentry.captureException(event.error, {
        tags: {
          component: 'ErrorBoundary'
        }
      });
    };

    // Listen for unhandled errors
    window.addEventListener('error', handleError);
    window.addEventListener('unhandledrejection', (event) => {
      handleError(new ErrorEvent('error', { error: event.reason }));
    });

    return () => {
      window.removeEventListener('error', handleError);
    };
  });
</script>

{#if hasError}
  <div class="error-boundary">
    <div class="error-content">
      <h2>Oops! Something went wrong</h2>
      <p>{fallback}</p>
      {#if error}
        <details class="error-details">
          <summary>Error Details</summary>
          <pre>{error.message}</pre>
          <pre>{error.stack}</pre>
        </details>
      {/if}
      <button 
        class="retry-button"
        on:click={() => window.location.reload()}
      >
        Reload Page
      </button>
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

  .error-details {
    margin: 1rem 0;
    text-align: left;
  }

  .error-details pre {
    background: #f5f5f5;
    padding: 0.5rem;
    border-radius: 4px;
    font-size: 0.875rem;
    overflow-x: auto;
  }

  .retry-button {
    background: #3b82f6;
    color: white;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 4px;
    cursor: pointer;
    margin-top: 1rem;
  }

  .retry-button:hover {
    background: #2563eb;
  }
</style> 