<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateMatch, CandidateSkill } from '@/features/candidates/api'
import type { VacancyRequirement } from '@/features/vacancies/api'
import { requirementMatched } from '../format'

const props = defineProps<{
  requirements: VacancyRequirement[]
  skills: CandidateSkill[]
  match: CandidateMatch
}>()

// Requirements render in vacancy order (ADR-0008 #13 / S4 spec).
const ordered = computed(() => [...props.requirements].sort((a, b) => a.position - b.position))

function isMatched(phrase: string): boolean {
  return requirementMatched(phrase, props.skills)
}
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default p-5 shadow-sm"
    aria-label="Requirements"
  >
    <div class="mb-3 flex items-baseline justify-between gap-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">Requirements</h2>
      <span class="text-sm font-semibold tabular-nums text-highlighted">
        {{ match.matchedRequirements }}/{{ match.totalRequirements }}
      </span>
    </div>
    <ul class="m-0 flex list-none flex-col gap-2 p-0">
      <li
        v-for="requirement in ordered"
        :key="requirement.id"
        class="flex items-center gap-2 text-sm"
      >
        <UIcon
          v-if="isMatched(requirement.phrase)"
          name="i-lucide-check"
          class="size-4 shrink-0 text-success"
          aria-label="Matched"
        />
        <span v-else class="size-4 shrink-0 text-center leading-4 text-muted" aria-hidden="true">
          ·
        </span>
        <span :class="isMatched(requirement.phrase) ? 'text-highlighted' : 'text-muted'">
          {{ requirement.phrase }}
        </span>
      </li>
    </ul>
  </section>
</template>
