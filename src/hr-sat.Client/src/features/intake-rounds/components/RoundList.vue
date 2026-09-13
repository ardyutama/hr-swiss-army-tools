<script setup lang="ts">
import { computed } from 'vue'
import type { VacancyRound } from '@/features/vacancies/api'
import { roundDisplayName } from '../useIntakeRounds'

const props = withDefaults(
  defineProps<{
    rounds: VacancyRound[]
    selectedRoundId: number | null
    canCreateRound?: boolean
    creating?: boolean
  }>(),
  { canCreateRound: false, creating: false },
)

const emit = defineEmits<{
  select: [round: VacancyRound]
  create: []
  close: [round: VacancyRound]
}>()

const orderedRounds = computed(() =>
  [...props.rounds].sort((left, right) => left.roundNumber - right.roundNumber),
)
</script>

<template>
  <section
    class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
    aria-label="Intake rounds"
  >
    <div class="flex items-center justify-between gap-3 border-b border-default px-5 py-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
        Intake rounds
      </h2>
      <UButton
        v-if="canCreateRound"
        color="primary"
        variant="subtle"
        size="sm"
        icon="i-lucide-plus"
        :loading="creating"
        @click="emit('create')"
      >
        New round
      </UButton>
    </div>

    <ul class="flex flex-col" role="list">
      <li v-for="round in orderedRounds" :key="round.id">
        <div
          class="group flex items-center gap-3 border-b border-default px-5 py-3 transition-colors last:border-b-0"
          :class="round.id === selectedRoundId ? 'bg-muted' : 'hover:bg-muted/60'"
        >
          <button
            type="button"
            class="flex min-w-0 flex-1 items-center gap-3 rounded-md text-left focus-visible:outline-2 focus-visible:outline-primary"
            :aria-current="round.id === selectedRoundId ? 'true' : undefined"
            @click="emit('select', round)"
          >
            <span class="min-w-0 flex-1 truncate text-sm font-medium text-highlighted">
              {{ roundDisplayName(round) }}
            </span>
            <UBadge
              :color="round.status === 'open' ? 'success' : 'neutral'"
              variant="subtle"
              class="gap-1.5"
            >
              <span class="size-1.5 rounded-full bg-current" aria-hidden="true" />
              {{ round.status === 'open' ? 'Open' : 'Closed' }}
            </UBadge>
            <span class="shrink-0 text-xs tabular-nums text-muted">
              {{ round.candidateCount }} {{ round.candidateCount === 1 ? 'candidate' : 'candidates' }}
            </span>
          </button>
          <UButton
            v-if="round.status === 'open'"
            color="neutral"
            variant="ghost"
            size="xs"
            icon="i-lucide-lock"
            :aria-label="`Close ${roundDisplayName(round)}`"
            @click="emit('close', round)"
          >
            Close
          </UButton>
        </div>
      </li>
    </ul>
  </section>
</template>
