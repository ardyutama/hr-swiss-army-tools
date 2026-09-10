<script setup lang="ts">
import { computed, nextTick, shallowRef, useTemplateRef, watch } from 'vue'
import { candidateDisplayName } from '@/features/candidates/format'
import type { CandidateHireOutcome } from '@/features/candidates/api'
import type { CandidateDetails, CandidateDetailsPayload } from '../api'
import { formatPromotionHistory } from '../format'
import { candidateDetailsValidationErrors } from '../validation'

const props = withDefaults(
  defineProps<{
    candidate: CandidateDetails
    saving?: boolean
    error?: string | null
  }>(),
  { saving: false, error: null },
)

const emit = defineEmits<{
  save: [payload: CandidateDetailsPayload]
}>()

const editing = shallowRef(false)
const draftFullName = shallowRef('')
const draftContactEmail = shallowRef('')
const fullNameInput = useTemplateRef<HTMLInputElement>('fullNameInput')

const hireOutcomeLabels: Record<Exclude<CandidateHireOutcome, 'none'>, string> = {
  hired: 'Hired',
  runaway: 'Runaway',
  declined: 'Declined',
}

const hireOutcomeColors: Record<Exclude<CandidateHireOutcome, 'none'>, 'success' | 'error' | 'neutral'> = {
  hired: 'success',
  runaway: 'error',
  declined: 'neutral',
}

function resetDraft() {
  draftFullName.value = props.candidate.fullName ?? props.candidate.sourceSenderName ?? ''
  draftContactEmail.value = props.candidate.contactEmail ?? props.candidate.sourceSenderEmail ?? ''
}

watch(() => props.candidate.id, resetDraft, { immediate: true })
watch(
  () => [props.candidate.fullName, props.candidate.contactEmail],
  () => {
    if (!editing.value) {
      resetDraft()
    }
  },
)
watch(
  () => props.saving,
  (saving, wasSaving) => {
    if (wasSaving && !saving && !props.error) {
      editing.value = false
    }
  },
)

const validationErrors = computed(() =>
  candidateDetailsValidationErrors(draftFullName.value, draftContactEmail.value),
)
const promotionHistory = computed(() =>
  formatPromotionHistory(props.candidate.promotedFromRoundNumber, props.candidate.promotedAt),
)

function startEditing() {
  resetDraft()
  editing.value = true
}

function focusEditing() {
  if (!editing.value) {
    startEditing()
  }

  void nextTick(() => fullNameInput.value?.focus())
}

function cancelEditing() {
  resetDraft()
  editing.value = false
}

function submit() {
  if (validationErrors.value.fullName || validationErrors.value.contactEmail) {
    return
  }

  emit('save', {
    fullName: draftFullName.value.trim(),
    contactEmail: draftContactEmail.value.trim(),
  })
}

defineExpose({ focusEditing })
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default p-5 shadow-sm"
    aria-label="Candidate details"
  >
    <div class="mb-3 flex items-center justify-between gap-3">
      <div class="flex min-w-0 items-center gap-2">
        <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
          Candidate details
        </h2>
        <UBadge
          v-if="candidate.hireOutcome !== 'none'"
          :color="hireOutcomeColors[candidate.hireOutcome]"
          variant="subtle"
        >
          {{ hireOutcomeLabels[candidate.hireOutcome] }}
        </UBadge>
      </div>
      <UButton
        v-if="!editing"
        color="neutral"
        variant="ghost"
        size="sm"
        icon="i-lucide-pencil"
        aria-label="Edit candidate details"
        aria-keyshortcuts="E"
        title="Edit candidate details (E)"
        @click="startEditing"
      >
        Edit
        <kbd
          class="ml-1 rounded border border-current/40 px-1.5 py-0.5 text-[0.65rem] font-semibold"
          aria-hidden="true"
        >E</kbd>
      </UButton>
    </div>

    <p v-if="props.error" class="mb-3 text-sm text-error" role="alert">{{ props.error }}</p>

    <form v-if="editing" class="flex flex-col gap-3" @submit.prevent="submit">
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="font-medium text-highlighted">Name</span>
        <input
          ref="fullNameInput"
          v-model="draftFullName"
          type="text"
          autocomplete="name"
          maxlength="300"
          aria-label="Candidate name"
          class="min-h-10 rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted outline-none focus:border-primary"
        />
        <span v-if="validationErrors.fullName" class="text-xs text-error">
          {{ validationErrors.fullName }}
        </span>
      </label>
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="font-medium text-highlighted">Email</span>
        <input
          v-model="draftContactEmail"
          type="email"
          autocomplete="email"
          maxlength="320"
          aria-label="Candidate email"
          class="min-h-10 rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted outline-none focus:border-primary"
        />
        <span v-if="validationErrors.contactEmail" class="text-xs text-error">
          {{ validationErrors.contactEmail }}
        </span>
      </label>
      <div class="flex justify-end gap-2">
        <UButton type="button" color="neutral" variant="ghost" :disabled="props.saving" @click="cancelEditing">
          Cancel
        </UButton>
        <UButton type="submit" color="primary" :loading="props.saving" :disabled="props.saving">
          Save details
        </UButton>
      </div>
    </form>

    <template v-else>
      <p class="text-xl font-bold tracking-tight text-highlighted">
        {{ candidateDisplayName(candidate) }}
      </p>
      <p v-if="promotionHistory" class="mt-1 text-sm text-muted">
        <UIcon name="i-lucide-arrow-up-right" class="mr-1 inline size-3.5" aria-hidden="true" />
        {{ promotionHistory }}
      </p>
      <dl class="mt-2 flex flex-col gap-1.5">
        <div class="flex items-center gap-2 text-sm text-muted">
          <UIcon name="i-lucide-mail" class="size-4 shrink-0" aria-hidden="true" />
          <dt class="sr-only">Email</dt>
          <dd class="m-0 truncate">{{ candidate.contactEmail ?? 'Not saved' }}</dd>
        </div>
      </dl>
      <p v-if="!candidate.fullName || !candidate.contactEmail" class="mt-3 text-sm text-muted">
        Details are not saved yet. Use the source email to complete them.
      </p>
    </template>
  </section>
</template>
