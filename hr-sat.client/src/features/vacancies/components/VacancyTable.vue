<script setup lang="ts">
import IconButton from '@/shared/ui/IconButton.vue'
import StatusBadge from './StatusBadge.vue'
import type { VacancySummary } from '../api'

defineProps<{
  rows: VacancySummary[]
}>()

const emit = defineEmits<{
  edit: [row: VacancySummary]
  remove: [row: VacancySummary]
}>()

function formatDate(iso: string): string {
  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? iso
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

function progressPercent(row: VacancySummary): number {
  const { processedCandidates, totalCandidates } = row.progress
  if (totalCandidates <= 0) {
    return 0
  }
  return Math.min(100, Math.round((processedCandidates / totalCandidates) * 100))
}
</script>

<template>
  <div class="vtable">
    <div class="vtable__head" role="row">
      <span class="vtable__col vtable__col--role">Vacancy</span>
      <span class="vtable__col vtable__col--status">Status</span>
      <span class="vtable__col vtable__col--progress">Candidates</span>
      <span class="vtable__col vtable__col--date">Opened</span>
      <span class="vtable__col vtable__col--actions"><span class="sr-only">Actions</span></span>
    </div>

    <ul class="vtable__body">
      <li v-for="row in rows" :key="row.id" class="vrow">
        <div class="vtable__col vtable__col--role">
          <span class="vrow__title">{{ row.title }}</span>
        </div>

        <div class="vtable__col vtable__col--status">
          <StatusBadge :status="row.status" />
        </div>

        <div class="vtable__col vtable__col--progress">
          <div class="vrow__progress">
            <div class="vrow__progress-track">
              <div class="vrow__progress-bar" :style="{ width: `${progressPercent(row)}%` }" />
            </div>
            <span class="vrow__progress-text">
              {{ row.progress.processedCandidates }}/{{ row.progress.totalCandidates }}
            </span>
          </div>
        </div>

        <div class="vtable__col vtable__col--date">
          <span class="vrow__date">{{ formatDate(row.openedOn) }}</span>
        </div>

        <div class="vtable__col vtable__col--actions">
          <div class="vrow__actions">
            <IconButton icon="pencil" label="Edit vacancy" @click="emit('edit', row)" />
            <IconButton icon="trash" label="Delete vacancy" variant="danger" @click="emit('remove', row)" />
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.vtable {
  display: flex;
  flex-direction: column;
}

.vtable__head {
  display: grid;
  grid-template-columns: minmax(0, 2.2fr) minmax(0, 1fr) minmax(0, 1.6fr) minmax(0, 1fr) 5rem;
  gap: var(--space-4);
  align-items: center;
  padding: 0 var(--space-5) var(--space-3);
  border-bottom: 1px solid var(--border);
}

.vtable__head .vtable__col {
  font-size: var(--text-xs);
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
}

.vtable__body {
  list-style: none;
  margin: 0;
  padding: 0;
}

.vrow {
  display: grid;
  grid-template-columns: minmax(0, 2.2fr) minmax(0, 1fr) minmax(0, 1.6fr) minmax(0, 1fr) 5rem;
  gap: var(--space-4);
  align-items: center;
  padding: var(--space-4) var(--space-5);
  border-bottom: 1px solid var(--border);
  transition: background 0.15s ease;
}

.vrow:last-child {
  border-bottom: none;
}

.vrow:hover {
  background: var(--surface-subtle);
}

.vtable__col--role {
  min-width: 0;
}

.vrow__title {
  display: block;
  font-size: var(--text-base);
  font-weight: 600;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.vrow__progress {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.vrow__progress-track {
  flex: 1;
  min-width: 3.5rem;
  height: 0.4rem;
  border-radius: var(--radius-pill);
  background: var(--neutral-chip-bg);
  overflow: hidden;
}

.vrow__progress-bar {
  height: 100%;
  border-radius: var(--radius-pill);
  background: var(--accent);
  transition: width 0.3s ease;
}

.vrow__progress-text {
  font-size: var(--text-sm);
  font-weight: 500;
  color: var(--muted);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.vrow__date {
  font-size: var(--text-sm);
  color: var(--muted);
  white-space: nowrap;
}

.vrow__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-1);
  opacity: 0;
  transition: opacity 0.15s ease;
}

.vrow:hover .vrow__actions,
.vrow:focus-within .vrow__actions {
  opacity: 1;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>

