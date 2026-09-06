<script setup lang="ts">
import { formatDate } from '@/features/vacancies/format'

defineProps<{
  vacancyId: string
  title: string
  openedOn: string
  /** 1-based position of the current candidate; 0 while unknown. */
  position: number
  total: number
  processed: number
  progressTotal: number
}>()
</script>

<template>
  <header class="flex items-center gap-3">
    <RouterLink
      :to="{ name: 'vacancy-detail', params: { id: vacancyId } }"
      aria-label="Back to candidate list"
      title="Back to candidate list"
      class="-ml-2 inline-flex size-10 shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-muted hover:text-highlighted"
    >
      <UIcon name="i-lucide-chevron-left" class="size-5" aria-hidden="true" />
    </RouterLink>
    <div class="min-w-0 flex-1">
      <h1 class="truncate text-xl font-bold tracking-tight text-highlighted">{{ title }}</h1>
      <p class="text-xs text-muted">Opened {{ formatDate(openedOn) }}</p>
    </div>
    <div class="shrink-0 text-right">
      <span
        class="block text-sm font-semibold tabular-nums text-highlighted"
        aria-label="Candidate position"
      >
        {{ position }} / {{ total }}
      </span>
      <span class="block text-xs tabular-nums text-muted" aria-label="Vacancy progress">
        {{ processed }} / {{ progressTotal }} reviewed
      </span>
    </div>
  </header>
</template>
