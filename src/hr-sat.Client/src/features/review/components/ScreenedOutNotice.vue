<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateScreening } from '../api'

const props = defineProps<{
  /** The candidate's screening disposition; the notice renders only when screened out. */
  screening: CandidateScreening | null
}>()

// Neutral chrome and the filter icon, never the warning triangle: screening
// is a computed disposition, not an alarm, and it never decides for HR.
const title = computed(() => {
  const count = props.screening?.firedRules.length ?? 0
  return count === 1 ? 'Screened out by 1 rule' : `Screened out by ${count} rules`
})
</script>

<template>
  <UAlert
    v-if="screening?.screenedOut"
    data-testid="screened-out-notice"
    color="neutral"
    variant="subtle"
    icon="i-lucide-filter"
    :title="title"
  >
    <template #description>
      <span class="flex flex-wrap items-center gap-1.5">
        <UBadge
          v-for="rule in screening.firedRules"
          :key="rule.index"
          color="neutral"
          variant="subtle"
        >
          {{ rule.display }}
        </UBadge>
      </span>
      <span class="mt-2 block">Screening never decides for you — review, flag, or shortlist as usual.</span>
    </template>
  </UAlert>
</template>
