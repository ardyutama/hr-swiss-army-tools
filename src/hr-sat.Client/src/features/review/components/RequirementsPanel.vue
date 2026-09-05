<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateRequirementReview } from '../api'
import type { VacancyRequirement } from '@/features/vacancies/api'

const props = defineProps<{
  requirements: VacancyRequirement[]
  reviews: CandidateRequirementReview[]
  savingRequirementId: number | null
  error: string | null
}>()

const emit = defineEmits<{
  toggle: [requirementId: number, confirmed: boolean]
}>()

// Requirements render in vacancy order (ADR-0008 #13 / S4 spec).
const ordered = computed(() => [...props.requirements].sort((a, b) => a.position - b.position))
const confirmedIds = computed(
  () =>
    new Set(
      props.reviews
        .filter((review) => review.confirmed)
        .map((review) => review.requirementId),
    ),
)
const confirmedCount = computed(() => confirmedIds.value.size)

function isConfirmed(requirementId: number): boolean {
  return confirmedIds.value.has(requirementId)
}

function toRequirementId(value: string): number {
  return Number(value)
}
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default p-5 shadow-sm"
    aria-label="Manual requirement review"
  >
    <div class="mb-3 flex items-baseline justify-between gap-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
        Requirement review
      </h2>
      <span class="text-sm font-semibold tabular-nums text-highlighted">
        {{ confirmedCount }} / {{ ordered.length }} confirmed
      </span>
    </div>
    <p v-if="error" class="mb-3 text-sm text-error" role="alert">{{ error }}</p>
    <ul class="m-0 flex list-none flex-col gap-2 p-0">
      <li
        v-for="requirement in ordered"
        :key="requirement.id"
        class="flex min-h-10 items-center gap-3 text-sm"
      >
        <label class="flex min-h-10 min-w-0 flex-1 cursor-pointer items-center gap-3">
          <input
            type="checkbox"
            :checked="isConfirmed(toRequirementId(requirement.id))"
            :disabled="savingRequirementId === toRequirementId(requirement.id)"
            :aria-label="`Confirm ${requirement.phrase} requirement`"
            class="size-4 shrink-0 accent-primary"
            @change="
              emit(
                'toggle',
                toRequirementId(requirement.id),
                ($event.target as HTMLInputElement).checked,
              )
            "
          />
          <span
            :class="
              isConfirmed(toRequirementId(requirement.id))
                ? 'font-medium text-highlighted'
                : 'text-muted'
            "
          >{{ requirement.phrase }}</span>
        </label>
      </li>
    </ul>
  </section>
</template>
