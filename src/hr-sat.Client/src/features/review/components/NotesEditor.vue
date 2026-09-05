<script setup lang="ts">
import { onMounted, useTemplateRef, watch } from 'vue'
import { formatClockTime } from '../format'
import { notesMaxLength } from '../validation'
import type { NotesSaveState } from '../useReview'

const notes = defineModel<string>({ default: '' })

const props = defineProps<{
  saveState: NotesSaveState
  savedAt: Date | null
  dirty: boolean
}>()

const emit = defineEmits<{
  save: []
}>()

const textarea = useTemplateRef<HTMLTextAreaElement>('textarea')

// Auto-growing textarea: grows with content up to ~8 lines, then scrolls (S4 spec).
function autogrow() {
  const element = textarea.value
  if (!element) {
    return
  }
  element.style.height = 'auto'
  element.style.height = `${element.scrollHeight}px`
}

watch(notes, autogrow)
onMounted(autogrow)
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default p-5 shadow-sm"
    aria-label="Notes"
  >
    <div class="mb-2 flex items-baseline justify-between gap-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">Notes</h2>
      <p class="m-0 text-xs text-muted" aria-live="polite">
        <span v-if="props.saveState === 'saving'">Saving…</span>
        <span v-else-if="props.saveState === 'saved' && props.savedAt">
          Saved {{ formatClockTime(props.savedAt) }}
        </span>
        <span v-else-if="props.dirty">Unsaved changes</span>
        <button
          v-else-if="props.saveState === 'error'"
          type="button"
          class="font-medium text-error underline underline-offset-2"
          @click="emit('save')"
        >
          Not saved — click to retry
        </button>
      </p>
    </div>
    <textarea
      ref="textarea"
      v-model="notes"
      :maxlength="notesMaxLength"
      rows="2"
      placeholder="Add notes about this candidate…"
      aria-label="Candidate notes"
      class="max-h-48 w-full resize-none overflow-y-auto rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted placeholder:text-muted focus:border-primary focus:outline-none"
    />
    <div class="mt-3 flex justify-end">
      <UButton
        color="primary"
        size="sm"
        :loading="props.saveState === 'saving'"
        :disabled="!props.dirty || props.saveState === 'saving'"
        @click="emit('save')"
      >
        Save notes
      </UButton>
    </div>
  </section>
</template>
