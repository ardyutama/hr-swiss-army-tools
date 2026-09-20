<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateListCounts } from '../api'
import type { CandidateOutcomeFilter, CandidateStatusFilter } from '../filter'

const props = withDefaults(
  defineProps<{
    /** Server-computed counts, rendered verbatim — no client recomputation. */
    counts: CandidateListCounts
    /** A form layout makes typed contact details searchable (decision 33). */
    hasFormLayout?: boolean
  }>(),
  { hasFormLayout: false },
)

const status = defineModel<CandidateStatusFilter>('status', { required: true })
const outcome = defineModel<CandidateOutcomeFilter>('outcome', { required: true })
const query = defineModel<string>('query', { required: true })
const screenedAll = defineModel<boolean>('screened', { required: true })

const chips: { value: CandidateStatusFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'new', label: 'New' },
  { value: 'flagged', label: 'Flagged' },
  { value: 'shortlisted', label: 'Shortlisted' },
  { value: 'rejected', label: 'Rejected' },
]

const outcomeChips: { value: CandidateOutcomeFilter; label: string }[] = [
  { value: 'any', label: 'Any outcome' },
  { value: 'undecided', label: 'Bench' },
  { value: 'hired', label: 'Hired' },
  { value: 'runaway', label: 'Runaway' },
  { value: 'declined', label: 'Declined' },
]

const showOutcomeChips = computed(() => status.value === 'all' || status.value === 'shortlisted')

// The All chip carries the non-screened funnel total; both count families come
// from the server payload.
const allCount = computed(() => props.counts.outcome.any)

// The toggle is unavailable-but-explained at zero (the Send-button precedent),
// never hidden — the toolbar stays spatially stable as imports land.
const screenedDisabled = computed(() => props.counts.screenedOut === 0)

const searchPlaceholder = computed(() =>
  props.hasFormLayout ? 'Name, email, or phone' : 'Name, sender email, or subject',
)

function countFor(value: CandidateStatusFilter): number {
  return value === 'all' ? allCount.value : props.counts.status[value]
}

function outcomeCountFor(value: CandidateOutcomeFilter): number {
  return props.counts.outcome[value]
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <!-- The toggle is pinned trailing on row one; the search wraps to its own
         line below `lg` (shared office PCs at 1280px hit the wrap). -->
    <div class="flex flex-wrap items-center gap-2">
      <div class="flex flex-wrap items-center gap-2" role="group" aria-label="Filter by review status">
        <UButton
          v-for="chip in chips"
          :key="chip.value"
          :color="status === chip.value ? 'primary' : 'neutral'"
          :variant="status === chip.value ? 'solid' : 'outline'"
          :aria-pressed="status === chip.value"
          size="sm"
          class="rounded-full"
          @click="status = chip.value"
        >
          {{ chip.label }}
          <span
            class="ml-1 tabular-nums"
            :class="status === chip.value ? 'opacity-75' : 'text-muted'"
          >{{ countFor(chip.value) }}</span>
        </UButton>
      </div>
      <UInput
        v-model="query"
        icon="i-lucide-search"
        :placeholder="searchPlaceholder"
        aria-label="Search candidates"
        class="order-last w-full lg:order-none lg:ml-auto lg:w-72"
      />
      <div class="ml-auto flex items-center gap-2 lg:ml-0">
        <UTooltip
          :text="'No screened-out candidates in this round.'"
          :disabled="!screenedDisabled"
        >
          <UCheckbox
            v-model="screenedAll"
            label="Show screened out"
            :disabled="screenedDisabled"
          />
        </UTooltip>
        <UBadge color="neutral" variant="subtle" class="tabular-nums">
          {{ counts.screenedOut }}
        </UBadge>
      </div>
    </div>
    <div
      v-if="showOutcomeChips"
      class="flex flex-wrap items-center gap-2"
      role="group"
      aria-label="Filter by hire outcome"
    >
      <span class="mr-1 text-xs font-semibold uppercase tracking-[0.06em] text-muted">Outcome</span>
      <UButton
        v-for="chip in outcomeChips"
        :key="chip.value"
        :color="outcome === chip.value ? 'primary' : 'neutral'"
        :variant="outcome === chip.value ? 'solid' : 'outline'"
        :aria-pressed="outcome === chip.value"
        size="sm"
        class="rounded-full"
        @click="outcome = chip.value"
      >
        {{ chip.label }}
        <span
          class="ml-1 tabular-nums"
          :class="outcome === chip.value ? 'opacity-75' : 'text-muted'"
        >{{ outcomeCountFor(chip.value) }}</span>
      </UButton>
    </div>
  </div>
</template>
