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

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
const columnWidths = ['32%', '13%', '25%', '13%', '5rem']
</script>

<template>
  <div class="overflow-x-auto">
    <table class="vtable w-full min-w-[52rem] table-fixed border-collapse">
      <colgroup>
        <col v-for="width in columnWidths" :key="width" :style="{ width }" />
      </colgroup>
      <thead class="vtable__head">
        <tr>
          <th scope="col" class="vtable__col vtable__col--role border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Vacancy</th>
          <th scope="col" class="vtable__col vtable__col--status border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Status</th>
          <th scope="col" class="vtable__col vtable__col--progress border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Candidates</th>
          <th scope="col" class="vtable__col vtable__col--date border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Opened</th>
          <th scope="col" class="vtable__col vtable__col--actions border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5"><span class="sr-only">Actions</span></th>
        </tr>
      </thead>

      <tbody>
        <tr v-for="row in rows" :key="row.id" class="vrow group">
          <td class="vtable__col vtable__col--role border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <RouterLink
              class="vrow__title vrow__title--link block truncate text-base font-semibold text-highlighted no-underline hover:text-primary hover:underline"
              :to="{ name: 'vacancy-detail', params: { id: row.id } }"
            >
              {{ row.title }}
            </RouterLink>
          </td>

          <td class="vtable__col vtable__col--status border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <StatusBadge :status="row.status" />
          </td>

          <td class="vtable__col vtable__col--progress border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="vrow__progress flex items-center gap-3">
              <div class="vrow__progress-track h-1.5 min-w-14 flex-1 overflow-hidden rounded-full bg-muted">
                <div
                  class="vrow__progress-bar h-full rounded-full bg-primary"
                  :style="{ width: `${progressPercent(row.progress)}%` }"
                />
              </div>
              <span class="vrow__progress-text whitespace-nowrap text-sm font-medium tabular-nums text-muted">
                {{ row.progress.processedCandidates }}/{{ row.progress.totalCandidates }}
              </span>
            </div>
          </td>

          <td class="vtable__col vtable__col--date border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="vrow__date whitespace-nowrap text-sm text-muted">{{ formatDate(row.openedOn) }}</span>
          </td>

          <td class="vtable__col vtable__col--actions border-b border-default px-2 py-4 text-right align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
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
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.vrow:last-child .vtable__col {
  border-bottom: none;
}

@media (hover: none) {
  .vrow__actions {
    opacity: 1;
  }
}
</style>

