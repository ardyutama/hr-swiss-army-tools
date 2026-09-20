<script setup lang="ts">
import { nextTick, onMounted, useTemplateRef, watch } from 'vue'
import { formatClockTime } from '../format'
import { applyNotePhrase, type NotePhrase } from '../notePhrases'
import { notesMaxLength } from '../validation'
import type { NotesSaveState } from '../useReview'

const notes = defineModel<string>({ default: '' })

const props = defineProps<{
  saveState: NotesSaveState
  savedAt: Date | null
  /** Gates the Save button only; no visible "unsaved" label (ADR-0008 #9). */
  dirty: boolean
}>()

const emit = defineEmits<{
  save: []
}>()

const textarea = useTemplateRef<HTMLTextAreaElement>('textarea')

function focus() {
  textarea.value?.focus()
}

function blur() {
  textarea.value?.blur()
}

function isFocused() {
  return textarea.value === document.activeElement
}

/**
 * One-click phrase insert shared by the chips and the Alt+digit shortcut.
 * Reads the live selection so a chip click can replace a marked span even
 * though the click itself blurred the textarea; restores a collapsed caret
 * right after the inserted phrase.
 */
function insertPhrase(phrase: NotePhrase) {
  const element = textarea.value
  const start = element?.selectionStart ?? notes.value.length
  const end = element?.selectionEnd ?? notes.value.length
  const result = applyNotePhrase(notes.value, start, end, phrase, notesMaxLength)
  notes.value = result.text
  void nextTick(() => {
    element?.focus()
    element?.setSelectionRange(result.cursor, result.cursor)
  })
}

defineExpose({ focus, blur, isFocused, insertPhrase })

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
      <div class="flex items-center gap-2">
        <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">Notes</h2>
        <span
          class="inline-flex items-center gap-1 text-muted"
          title="Press N to focus Notes. Press Escape to save and leave."
        >
          <kbd
            class="rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
            aria-hidden="true"
          >N</kbd>
          <span class="sr-only">Press N to focus Notes. Press Escape to save and leave Notes.</span>
        </span>
      </div>
      <p class="m-0 text-xs text-muted" aria-live="polite">
        <span v-if="props.saveState === 'saving'">Saving…</span>
        <span v-else-if="props.saveState === 'saved' && props.savedAt">
          Saved {{ formatClockTime(props.savedAt) }}
        </span>
        <button
          v-else-if="props.saveState === 'error'"
          type="button"
          class="font-medium text-error underline underline-offset-2"
          @click="emit('save')"
        >
          Not saved - click to retry
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
    <!-- Phrase chips (NotePhraseChips) sit below the textarea, inside the card. -->
    <div v-if="$slots.default" class="mt-3">
      <slot />
    </div>
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
