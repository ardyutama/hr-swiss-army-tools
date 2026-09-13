<script setup lang="ts">
import type { CandidateSummary } from '@/features/candidates/api'
import { candidateDisplayName } from '@/features/candidates/format'
import type { VacancyRound } from '@/features/vacancies/api'
import { roundDisplayName } from '@/features/intake-rounds/useIntakeRounds'

const open = defineModel<boolean>('open', { required: true })
const sourceRoundId = defineModel<number | null>('sourceRoundId', { required: true })
const selectedCandidateIds = defineModel<number[]>('selectedCandidateIds', { required: true })

const props = withDefaults(
  defineProps<{
    rounds: VacancyRound[]
    promotableCandidates: CandidateSummary[]
    loading?: boolean
    error?: string | null
    submitting?: boolean
    submitError?: string | null
    canSubmit?: boolean
  }>(),
  {
    loading: false,
    error: null,
    submitting: false,
    submitError: null,
    canSubmit: false,
  },
)

const emit = defineEmits<{
  submit: []
}>()

function isSelected(candidateId: number): boolean {
  return selectedCandidateIds.value.includes(candidateId)
}

function toggleCandidate(candidateId: number, selected: boolean) {
  if (selected) {
    if (!isSelected(candidateId)) {
      selectedCandidateIds.value = [...selectedCandidateIds.value, candidateId]
    }
    return
  }
  selectedCandidateIds.value = selectedCandidateIds.value.filter((id) => id !== candidateId)
}
</script>

<template>
  <UModal
    v-model:open="open"
    title="Promote candidates"
    :dismissible="!props.submitting"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <label class="flex flex-col gap-1.5 text-sm">
          <span class="font-medium text-highlighted">Promote from</span>
          <select
            v-model.number="sourceRoundId"
            aria-label="Source round"
            :disabled="props.loading || props.submitting"
            class="min-h-10 rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted outline-none focus:border-primary disabled:cursor-not-allowed disabled:opacity-60"
          >
            <option v-for="round in props.rounds" :key="round.id" :value="round.id">
              {{ roundDisplayName(round) }}
            </option>
          </select>
        </label>

        <p class="text-sm text-muted">
          Select candidates to move into the active round. Their review status, notes, and requirement reviews stay with them.
        </p>

        <UAlert
          v-if="props.error"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Couldn't load candidates"
          :description="props.error"
          role="alert"
        />

        <div v-else-if="props.loading" class="flex items-center gap-2 py-4 text-sm text-muted" aria-busy="true">
          <UIcon name="i-lucide-loader-circle" class="size-4 animate-spin" aria-hidden="true" />
          Loading candidates…
        </div>

        <p v-else-if="props.promotableCandidates.length === 0" class="rounded-lg border border-default px-4 py-4 text-sm text-muted">
          No promotable candidates in this round.
        </p>

        <fieldset v-else class="flex flex-col gap-1">
          <legend class="sr-only">Promotable candidates</legend>
          <label
            v-for="candidate in props.promotableCandidates"
            :key="candidate.id"
            class="flex min-h-10 cursor-pointer items-center gap-3 rounded-lg px-2 py-2 text-sm transition-colors hover:bg-muted"
          >
            <input
              type="checkbox"
              :checked="isSelected(candidate.id)"
              :disabled="props.submitting"
              :aria-label="`Promote ${candidateDisplayName(candidate)}`"
              class="size-4 shrink-0 accent-primary"
              @change="toggleCandidate(candidate.id, ($event.target as HTMLInputElement).checked)"
            />
            <span class="min-w-0 flex-1 truncate font-medium text-highlighted">
              {{ candidateDisplayName(candidate) }}
            </span>
            <span class="shrink-0 text-xs text-muted capitalize">{{ candidate.reviewStatus }}</span>
          </label>
        </fieldset>

        <UAlert
          v-if="props.submitError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Couldn't promote candidates"
          :description="props.submitError"
          role="alert"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex items-center justify-between gap-3">
        <span class="text-sm text-muted">
          {{ selectedCandidateIds.length }} selected
        </span>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="outline" :disabled="props.submitting" @click="open = false">
            Cancel
          </UButton>
          <UButton color="primary" :loading="props.submitting" :disabled="!props.canSubmit" @click="emit('submit')">
            Promote selected
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>