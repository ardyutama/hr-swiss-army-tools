<script setup lang="ts">
import type { VacancySummary } from '../api'

withDefaults(
  defineProps<{
    open: boolean
    vacancy: VacancySummary | null
    deleting?: boolean
    error?: string | null
  }>(),
  { deleting: false, error: null },
)

const emit = defineEmits<{
  close: []
  confirm: []
}>()
</script>

<template>
  <UModal
    :open="open"
    title="Delete vacancy"
    :dismissible="!deleting"
    @update:open="(value) => { if (!value) emit('close') }"
  >
    <template #body>
      <p class="text-base leading-relaxed">
        Delete <strong class="font-semibold">{{ vacancy?.title }}</strong
        >? This permanently removes the vacancy and can't be undone.
      </p>
      <UAlert
        v-if="error"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="error"
        role="alert"
        class="mt-3"
      />
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="deleting" @click="emit('close')">
          Cancel
        </UButton>
        <UButton color="error" :loading="deleting" @click="emit('confirm')">
          Delete vacancy
        </UButton>
      </div>
    </template>
  </UModal>
</template>
