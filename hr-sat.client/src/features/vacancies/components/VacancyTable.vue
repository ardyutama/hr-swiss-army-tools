<script setup lang="ts">
import StatusBadge from './StatusBadge.vue'
import type { VacancySummary } from '../api'
import { formatDate, progressPercent } from '../format'

defineProps<{
  rows: VacancySummary[]
}>()

const emit = defineEmits<{
  edit: [row: VacancySummary]
  remove: [row: VacancySummary]
}>()
</script>

<template>
  <div class="overflow-x-auto">
    <div class="vtable flex min-w-[52rem] flex-col">
      <div class="vtable__head grid grid-cols-[minmax(0,2.2fr)_minmax(0,1fr)_minmax(0,1.6fr)_minmax(0,1fr)_5rem] items-center gap-4 border-b border-default px-5 pb-3">
        <span class="vtable__col vtable__col--role text-xs font-semibold uppercase tracking-[0.06em] text-muted">Vacancy</span>
        <span class="vtable__col vtable__col--status text-xs font-semibold uppercase tracking-[0.06em] text-muted">Status</span>
        <span class="vtable__col vtable__col--progress text-xs font-semibold uppercase tracking-[0.06em] text-muted">Candidates</span>
        <span class="vtable__col vtable__col--date text-xs font-semibold uppercase tracking-[0.06em] text-muted">Opened</span>
        <span class="vtable__col vtable__col--actions text-xs font-semibold uppercase tracking-[0.06em] text-muted"><span class="sr-only">Actions</span></span>
      </div>

      <ul class="vtable__body m-0 list-none p-0">
        <li v-for="row in rows" :key="row.id" class="vrow group grid grid-cols-[minmax(0,2.2fr)_minmax(0,1fr)_minmax(0,1.6fr)_minmax(0,1fr)_5rem] items-center gap-4 border-b border-default px-5 py-4 transition-colors last:border-b-0 hover:bg-muted">
          <div class="vtable__col vtable__col--role min-w-0">
            <RouterLink
              class="vrow__title vrow__title--link block truncate text-base font-semibold text-highlighted no-underline hover:text-primary hover:underline"
              :to="{ name: 'vacancy-detail', params: { id: row.id } }"
            >
              {{ row.title }}
            </RouterLink>
          </div>

          <div class="vtable__col vtable__col--status">
            <StatusBadge :status="row.status" />
          </div>

          <div class="vtable__col vtable__col--progress">
            <div class="vrow__progress flex items-center gap-3">
              <div class="vrow__progress-track h-1.5 min-w-14 flex-1 overflow-hidden rounded-full bg-muted">
                <div
                  class="vrow__progress-bar h-full rounded-full bg-primary transition-[width] duration-300"
                  :style="{ width: `${progressPercent(row.progress)}%` }"
                />
              </div>
              <span class="vrow__progress-text whitespace-nowrap text-sm font-medium tabular-nums text-muted">
                {{ row.progress.processedCandidates }}/{{ row.progress.totalCandidates }}
              </span>
            </div>
          </div>

          <div class="vtable__col vtable__col--date">
            <span class="vrow__date whitespace-nowrap text-sm text-muted">{{ formatDate(row.openedOn) }}</span>
          </div>

          <div class="vtable__col vtable__col--actions">
            <div
              v-if="row.status === 'open'"
              class="vrow__actions flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
            >
              <UButton
                icon="i-lucide-pencil"
                color="neutral"
                variant="ghost"
                size="sm"
                aria-label="Edit vacancy"
                title="Edit vacancy"
                @click="emit('edit', row)"
              />
              <UButton
                icon="i-lucide-trash-2"
                color="error"
                variant="ghost"
                size="sm"
                aria-label="Delete vacancy"
                title="Delete vacancy"
                @click="emit('remove', row)"
              />
            </div>
          </div>
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped>
@media (hover: none) {
  .vrow__actions {
    opacity: 1;
  }
}
</style>

