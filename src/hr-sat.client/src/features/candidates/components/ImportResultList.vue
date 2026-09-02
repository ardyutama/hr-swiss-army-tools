<script setup lang="ts">
import type { ImportFileResult, ImportFileStatus } from '@/features/candidates/api'

defineProps<{
  results: ImportFileResult[]
}>()

const statusLabels: Record<ImportFileStatus, string> = {
  imported: 'Imported',
  skipped: 'Skipped',
  failed: 'Failed',
}

const statusColors: Record<ImportFileStatus, 'success' | 'neutral' | 'error'> = {
  imported: 'success',
  skipped: 'neutral',
  failed: 'error',
}
</script>

<template>
  <ul class="results flex flex-col" aria-label="Import results">
    <li
      v-for="(result, index) in results"
      :key="`${result.fileName}-${index}`"
      class="results__row flex items-center justify-between gap-4 border-b border-default px-5 py-3 last:border-b-0"
    >
      <div class="results__main flex min-w-0 flex-col gap-0.5">
        <span class="results__name block truncate text-sm font-semibold text-highlighted">{{ result.fileName }}</span>
        <span v-if="result.error" class="results__error text-xs text-muted">{{ result.error }}</span>
      </div>
      <UBadge :color="statusColors[result.status]" variant="subtle" class="shrink-0">
        {{ statusLabels[result.status] }}
      </UBadge>
    </li>
  </ul>
</template>
