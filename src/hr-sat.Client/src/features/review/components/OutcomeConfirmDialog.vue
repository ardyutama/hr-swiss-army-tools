<script setup lang="ts">
import { nextTick, shallowRef, useTemplateRef, watch } from 'vue'
import type { CandidateHireOutcome } from '@/features/candidates/api'
import { notesMaxLength } from '../validation'

type ConsequentialOutcome = Extract<CandidateHireOutcome, 'runaway' | 'declined'>

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    outcome: ConsequentialOutcome | null
    busy?: boolean
    error?: string | null
  }>(),
  { busy: false, error: null },
)

const emit = defineEmits<{
  confirm: [note: string]
}>()

const note = shallowRef('')
const textarea = useTemplateRef<HTMLTextAreaElement>('textarea')

watch(open, async (isOpen) => {
  if (!isOpen) {
    textarea.value?.blur()
    return
  }
  note.value = ''
  await nextTick()
  textarea.value?.focus()
})

function confirm() {
  if (!props.busy) {
    emit('confirm', note.value.trim())
  }
}

function cancel() {
  if (!props.busy) {
    open.value = false
  }
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="props.outcome === 'runaway' ? 'Mark Runaway' : 'Mark Declined'"
    :dismissible="!props.busy"
  >
    <template #body>
      <form class="flex flex-col gap-4" @submit.prevent="confirm">
        <p class="m-0 text-base leading-relaxed text-highlighted">
          <template v-if="props.outcome === 'runaway'">
            Reopens 1 needed-hire slot.
          </template>
          <template v-else>
            Does not reopen a slot.
          </template>
        </p>
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-highlighted">Note <span class="text-muted">(optional)</span></span>
          <textarea
            ref="textarea"
            v-model="note"
            :maxlength="notesMaxLength"
            rows="4"
            aria-label="Outcome note"
            placeholder="Add a note about this outcome"
            class="w-full resize-y rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted placeholder:text-muted focus:border-primary focus:outline-none"
            @keydown.enter.exact.prevent="confirm"
            @keydown.esc.stop.prevent="cancel"
          />
        </label>
        <UAlert
          v-if="props.error"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="props.error"
          role="alert"
        />
      </form>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.busy" @click="cancel">
          Cancel
        </UButton>
        <UButton
          :color="props.outcome === 'runaway' ? 'error' : 'neutral'"
          :loading="props.busy"
          @click="confirm"
        >
          {{ props.outcome === 'runaway' ? 'Mark Runaway' : 'Mark Declined' }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
