<script setup lang="ts">
import { notePhraseGroups, type NotePhrase } from '../notePhrases'

/**
 * The two chip rows below the Notes editor (glossary-free client aid):
 * Replacements overwrite the selection or the whole draft, Additionals insert
 * at the caret. Click is the primary path; Alt+<chip number> fires while
 * Notes is focused — the kbd hint advertises it (ADR-0008 #8).
 */
const emit = defineEmits<{
  insert: [phrase: NotePhrase]
}>()
</script>

<template>
  <div class="flex flex-col gap-2" aria-label="Note phrases">
    <div
      v-for="group in notePhraseGroups"
      :key="group.id"
      class="flex flex-wrap items-center gap-2"
    >
      <span class="text-xs text-muted">{{ group.label }}:</span>
      <UButton
        v-for="phrase in group.phrases"
        :key="phrase.index"
        size="xs"
        color="neutral"
        variant="soft"
        :title="`Press Alt+${phrase.index} while Notes is focused`"
        @click="emit('insert', phrase)"
      >
        {{ phrase.text }}
        <kbd
          class="rounded border border-current/40 px-1 text-[0.6rem] font-semibold opacity-70"
          aria-hidden="true"
        >Alt+{{ phrase.index }}</kbd>
      </UButton>
    </div>
  </div>
</template>
