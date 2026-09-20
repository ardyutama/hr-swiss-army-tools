<script setup lang="ts">
import { computed } from 'vue'
import type { ScreeningRulesViewState } from '../useScreeningRules'

const props = withDefaults(
  defineProps<{
    state: ScreeningRulesViewState
    ruleCount: number
    loadError?: string | null
    /** Rules point at header-snapshot columns; before the first import there is nothing to point at. */
    hasHeaderSnapshot: boolean
    /** Screened-out count of the viewed round; null when no round is selected. */
    screenedOutCount: number | null
    /** Display name of the viewed round; null when no round is selected. */
    roundName: string | null
  }>(),
  { loadError: null },
)

const emit = defineEmits<{
  edit: []
  retry: []
}>()

// The button stays rendered (disabled with the reason in the summary line)
// before the first import — unavailable-but-explained, never hidden. A closed
// vacancy keeps it enabled: the dialog opens read-only.
const canEdit = computed(() => props.hasHeaderSnapshot)

const summaryLine = computed(() => {
  if (!props.hasHeaderSnapshot) {
    return 'Import form responses to set up screening rules.'
  }
  if (props.ruleCount === 0) {
    return 'No screening rules.'
  }
  const rulesClause = props.ruleCount === 1 ? '1 rule' : `${props.ruleCount} rules`
  // The count clause counts the viewed round and names it; it drops when no
  // round is selected, and never reuses the editor's "in the active round".
  if (props.roundName === null || props.screenedOutCount === null) {
    return rulesClause
  }
  return `${rulesClause} · ${props.screenedOutCount} screened out in ${props.roundName}`
})
</script>

<template>
  <section
    class="overflow-hidden rounded-xl border border-default bg-default shadow-sm"
    aria-label="Screening"
  >
    <div class="flex items-center justify-between gap-3 border-b border-default px-5 py-3">
      <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
        Screening
      </h2>
      <UButton
        v-if="props.state === 'ready' || props.state === 'empty'"
        color="primary"
        variant="subtle"
        size="sm"
        icon="i-lucide-filter"
        :disabled="!canEdit"
        @click="emit('edit')"
      >
        Screening rules
      </UButton>
    </div>

    <div
      v-if="props.state === 'loading'"
      class="flex flex-col gap-3 px-5 py-4"
      aria-busy="true"
      aria-label="Loading screening rules"
    >
      <USkeleton class="h-4 w-2/3" />
    </div>

    <div v-else-if="props.state === 'error'" class="px-5 py-4">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        title="Couldn't load the screening rules"
        :description="props.loadError ?? undefined"
        role="alert"
        :actions="[
          { label: 'Retry', color: 'error', variant: 'outline', onClick: () => emit('retry') },
        ]"
      />
    </div>

    <p v-else class="px-5 py-4 text-sm text-muted">
      {{ summaryLine }}
    </p>
  </section>
</template>
