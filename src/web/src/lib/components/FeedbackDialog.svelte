<script lang="ts">
  import * as Sentry from '@sentry/sveltekit';

  // Sentry ships a feedback widget, but its form cannot say which error a report belongs to —
  // associatedEventId is a parameter of captureFeedback, not an option on the widget.
  export let open = false;

  let dialog: HTMLDialogElement;
  let submitted = false;
  let name = '';
  let email = '';
  let message = '';

  $: if (dialog) {
    if (open && !dialog.open) {
      submitted = false;
      message = '';
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }

  const submitFeedback = (event: Event) => {
    event.preventDefault();
    Sentry.captureFeedback({
      name: name || undefined,
      email: email || undefined,
      message,
      // Ties the report to the error the user is looking at, so it lands on that issue rather
      // than arriving as a free-floating comment.
      associatedEventId: Sentry.lastEventId()
    });
    submitted = true;
  };
</script>

<dialog bind:this={dialog} class="feedback-dialog" on:close={() => (open = false)}>
  {#if submitted}
    <p>Thanks — that will help us fix it.</p>
    <button type="button" on:click={() => (open = false)}>Close</button>
  {:else}
    <form on:submit={submitFeedback}>
      <h2>Tell us what happened</h2>
      <label>
        What were you doing when this happened?
        <textarea bind:value={message} required rows="3"></textarea>
      </label>
      <label>
        Your name (optional)
        <input type="text" bind:value={name} />
      </label>
      <label>
        Your email (optional)
        <input type="email" bind:value={email} />
      </label>
      <div class="actions">
        <button type="button" on:click={() => (open = false)}>Cancel</button>
        <button type="submit">Send report</button>
      </div>
    </form>
  {/if}
</dialog>

<style>
  .feedback-dialog {
    border: none;
    border-radius: 0.5rem;
    padding: 1.5rem;
    max-width: 28rem;
    width: 100%;
  }

  .feedback-dialog form {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .feedback-dialog label {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    font-size: 0.875rem;
  }

  .actions {
    display: flex;
    gap: 0.5rem;
    justify-content: flex-end;
  }
</style>
