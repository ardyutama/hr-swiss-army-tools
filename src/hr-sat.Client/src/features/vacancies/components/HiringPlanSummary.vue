<script setup lang="ts">
import { computed } from 'vue'
import type { VacancyHiring } from '../api'
import { hiringProgressText, hiringShortage, isFilled } from '../hiring'

const props = withDefaults(
  defineProps<{
    hiring?: VacancyHiring | null
    variant?: 'summary' | 'context'
  }>(),
  { variant: 'summary' },
)

const shortage = computed(() => {
  const hiring = props.hiring
  return hiring ? hiringShortage(hiring) : null
})

const filled = computed(() => (props.hiring ? isFilled(props.hiring) : false))
</script>

<template>
  <section
    v-if="props.variant === 'summary'"
    class="rounded-xl border border-default bg-default px-5 py-4 shadow-sm"
    aria-label="Hiring plan"
  >
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h2 class="text-sm font-semibold text-highlighted">Hiring plan</h2>
        <p v-if="props.hiring" class="mt-1 text-sm text-muted">Staffing target for this vacancy</p>
        <p v-else class="mt-1 text-sm text-muted">No hiring target set</p>
      </div>
      <UBadge v-if="filled" color="success" variant="subtle">Filled</UBadge>
    </div>

    <dl v-if="props.hiring" class="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
      <div>
        <dt class="text-xs font-medium text-muted">Needed hires</dt>
        <dd class="mt-1 text-xl font-semibold tabular-nums text-highlighted">{{ props.hiring.neededHires }}</dd>
      </div>
      <div>
        <dt class="text-xs font-medium text-muted">Active hires</dt>
        <dd class="mt-1 text-xl font-semibold tabular-nums text-highlighted">{{ props.hiring.activeHires }}</dd>
      </div>
      <div>
        <dt class="text-xs font-medium text-muted">Still needed</dt>
        <dd class="mt-1 text-xl font-semibold tabular-nums text-highlighted">{{ shortage }}</dd>
      </div>
    </dl>
  </section>

  <section
    v-else
    class="border-y border-default px-5 py-3"
    aria-label="Candidate workspace hiring plan"
  >
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm">
      <span class="font-semibold text-highlighted">Hiring plan</span>
      <template v-if="props.hiring">
        <span class="text-muted tabular-nums">{{ hiringProgressText(props.hiring) }}</span>
        <UBadge v-if="filled" color="success" variant="subtle">Filled</UBadge>
      </template>
      <span v-else class="text-muted">No hiring target set</span>
    </div>
  </section>
</template>