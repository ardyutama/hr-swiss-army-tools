<script setup lang="ts">
import type { CandidateListState } from '@/features/candidates/useCandidateFilter'

defineProps<{
  state: CandidateListState
  canOpenRound: boolean
}>()

const emit = defineEmits<{
  'open-round': []
  import: []
  'clear-filters': []
}>()
</script>

<template>
  <UEmpty
    v-if="state.kind === 'ready'"
    icon="i-lucide-search-x"
    title="No candidates match these filters"
    description="Try a different search or clear the filters."
    class="min-h-40 px-6 py-10"
    :actions="[{ label: 'Clear filters', icon: 'i-lucide-x', onClick: () => emit('clear-filters') }]"
  />
  <template v-else-if="state.kind === 'empty'">
    <!-- Closed vacancy is read-only -->
    <UEmpty
      v-if="state.reason === 'vacancy-closed'"
      icon="i-lucide-lock-keyhole"
      title="This vacancy is closed"
      description="A closed vacancy is read-only and can't receive candidate imports."
      class="min-h-48 px-6 py-10"
    />
    <!-- No active round: imports are rejected until a round is opened -->
    <UEmpty
      v-else-if="state.reason === 'no-active-round'"
      icon="i-lucide-archive"
      title="No active round"
      description="Open a new intake round before importing candidates."
      class="min-h-48 px-6 py-10"
      :actions="canOpenRound ? [{ label: 'Open a round', icon: 'i-lucide-plus', onClick: () => emit('open-round') }] : []"
    />
    <!-- Selected round is closed (read-only) -->
    <UEmpty
      v-else-if="state.reason === 'round-closed'"
      icon="i-lucide-lock-keyhole"
      title="This round is closed"
      description="A closed round is read-only. Open a new round to keep importing."
      class="min-h-48 px-6 py-10"
    />
    <UEmpty
      v-else
      icon="i-lucide-users"
      title="No candidates yet"
      description="Export the application emails as .eml files and drop them in to import each email as a candidate."
      class="min-h-48 px-6 py-10"
      :actions="[{ label: 'Import .eml files', icon: 'i-lucide-upload', onClick: () => emit('import') }]"
    />
  </template>
</template>
