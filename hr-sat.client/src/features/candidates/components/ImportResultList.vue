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
</script>

<template>
  <ul class="results" aria-label="Import results">
    <li v-for="(result, index) in results" :key="`${result.fileName}-${index}`" class="results__row">
      <div class="results__main">
        <span class="results__name">{{ result.fileName }}</span>
        <span v-if="result.error" class="results__error">{{ result.error }}</span>
      </div>
      <span class="results__status" :class="`results__status--${result.status}`">
        {{ statusLabels[result.status] }}
      </span>
    </li>
  </ul>
</template>

<style scoped>
.results {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.results__row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) var(--space-5);
  border-bottom: 1px solid var(--border);
}

.results__row:last-child {
  border-bottom: none;
}

.results__main {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.results__name {
  font-size: var(--text-sm);
  font-weight: 600;
  color: var(--text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.results__error {
  font-size: var(--text-xs);
  color: var(--muted);
}

.results__status {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.65rem;
  border-radius: var(--radius-pill);
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.01em;
}

.results__status--imported {
  background: var(--success-soft);
  color: var(--success);
}

.results__status--skipped {
  background: var(--neutral-chip-bg);
  color: var(--neutral-chip-text);
}

.results__status--failed {
  background: var(--danger-soft);
  color: var(--danger);
}
</style>
