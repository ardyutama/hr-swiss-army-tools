<script setup lang="ts">
import StatusBadge from './StatusBadge.vue'
import type { VacancySummary } from '../api'
import type { SortDirection, VacancySortKey } from '../useVacancies'
import { formatDate, progressPercent, reviewCountsText } from '../format'
import { hiringProgressText, isFilled } from '../hiring'

const props = defineProps<{
  rows: VacancySummary[]
  sortKey: VacancySortKey | null
  sortDirection: SortDirection
}>()

const emit = defineEmits<{
  edit: [row: VacancySummary]
  remove: [row: VacancySummary]
  sort: [key: VacancySortKey]
}>()

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
const columnWidths = ['24%', '10%', '34%', '14%', '12%', '5rem']

function ariaSort(key: VacancySortKey): 'ascending' | 'descending' | 'none' {
  if (props.sortKey !== key) {
    return 'none'
  }
  return props.sortDirection === 'asc' ? 'ascending' : 'descending'
}

function sortIcon(key: VacancySortKey): string {
  if (props.sortKey !== key) {
    return 'i-lucide-chevrons-up-down'
  }
  return props.sortDirection === 'asc' ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'
}
</script>

<template>
  <div class="overflow-x-auto">
    <table class="vtable w-full min-w-[52rem] table-fixed border-collapse">
      <colgroup>
        <col v-for="width in columnWidths" :key="width" :style="{ width }" />
      </colgroup>
      <thead class="vtable__head">
        <tr>
          <th scope="col" :aria-sort="ariaSort('title')" class="vtable__col vtable__col--role border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">
            <button type="button" class="vtable__sort inline-flex cursor-pointer items-center gap-1 uppercase tracking-[0.06em] transition-colors hover:text-highlighted" @click="emit('sort', 'title')">
              Vacancy
              <UIcon :name="sortIcon('title')" class="size-3.5 shrink-0" />
            </button>
          </th>
          <th scope="col" class="vtable__col vtable__col--status border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Status</th>
          <th scope="col" :aria-sort="ariaSort('progress')" class="vtable__col vtable__col--progress border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">
            <button type="button" class="vtable__sort inline-flex cursor-pointer items-center gap-1 uppercase tracking-[0.06em] transition-colors hover:text-highlighted" @click="emit('sort', 'progress')">
              Progress
              <UIcon :name="sortIcon('progress')" class="size-3.5 shrink-0" />
            </button>
          </th>
          <th scope="col" class="vtable__col vtable__col--hiring border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">Hiring</th>
          <th scope="col" :aria-sort="ariaSort('opened')" class="vtable__col vtable__col--date border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5">
            <button type="button" class="vtable__sort inline-flex cursor-pointer items-center gap-1 uppercase tracking-[0.06em] transition-colors hover:text-highlighted" @click="emit('sort', 'opened')">
              Opened
              <UIcon :name="sortIcon('opened')" class="size-3.5 shrink-0" />
            </button>
          </th>
          <th scope="col" class="vtable__col vtable__col--actions border-b border-default px-2 pb-3 pt-4 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5"><span class="sr-only">Actions</span></th>
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
            <div class="flex flex-col gap-1.5">
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
              <div
                v-if="reviewCountsText(row.reviewCounts)"
                class="vrow__review-counts text-xs tabular-nums text-muted"
              >
                {{ reviewCountsText(row.reviewCounts) }}
              </div>
            </div>
          </td>

          <td class="vtable__col vtable__col--hiring border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div v-if="row.hiring" class="flex flex-col gap-1.5">
              <span class="vrow__hiring whitespace-nowrap text-sm tabular-nums text-muted">{{ hiringProgressText(row.hiring) }}</span>
              <div>
                <UBadge v-if="isFilled(row.hiring)" color="success" variant="subtle">
                  Filled
                </UBadge>
              </div>
            </div>
            <span v-else class="vrow__hiring vrow__hiring--none text-sm text-muted">—</span>
          </td>

          <td class="vtable__col vtable__col--date border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="vrow__date whitespace-nowrap text-sm text-muted">{{ formatDate(row.openedOn) }}</span>
          </td>

          <td class="vtable__col vtable__col--actions border-b border-default px-2 py-4 text-right align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div
              class="vrow__actions flex justify-end gap-1 opacity-0 transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
            >
              <UButton
                v-if="row.status === 'open'"
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
                aria-label="Purge vacancy"
                title="Purge vacancy"
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

