<script setup lang="ts">
import type { VacancyDetails } from '@/features/vacancies/api'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    vacancy: VacancyDetails | null
    closing?: boolean
  }>(),
  { closing: false },
)

const emit = defineEmits<{
  confirm: []
}>()
</script>

<template>
  <UModal v-model:open="open" title="Close vacancy" :dismissible="!props.closing">
    <template #body>
      <div class="flex flex-col gap-4">
        <div
          class="flex items-center justify-between gap-3 rounded-xl border border-default bg-muted/30 px-4 py-3"
        >
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold text-highlighted">{{ vacancy?.title }}</p>
            <p class="text-sm tabular-nums text-muted">
              {{ vacancy?.progress.processedCandidates }}/{{ vacancy?.progress.totalCandidates }}
              candidates processed
            </p>
          </div>
          <UBadge color="success" variant="subtle" class="shrink-0">Open</UBadge>
        </div>

        <p class="text-sm leading-relaxed text-muted">
          Closing makes the vacancy read-only &mdash; no edits, no imports, no new rounds, and hire
          outcomes freeze. You can reopen it explicitly later.
        </p>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.closing" @click="open = false">
          Cancel
        </UButton>
        <UButton color="error" icon="i-lucide-lock" :loading="props.closing" @click="emit('confirm')">
          Close vacancy
        </UButton>
      </div>
    </template>
  </UModal>
</template>
