<script setup lang="ts">
import AppButton from '@/shared/ui/AppButton.vue'
import AppDialog from '@/shared/ui/AppDialog.vue'
import type { VacancySummary } from '../api'

withDefaults(
  defineProps<{
    open: boolean
    vacancy: VacancySummary | null
    deleting?: boolean
  }>(),
  { deleting: false },
)

const emit = defineEmits<{
  close: []
  confirm: []
}>()
</script>

<template>
  <AppDialog :open="open" title="Delete vacancy" @close="emit('close')">
    <p class="confirm__text">
      Delete <strong>{{ vacancy?.title }}</strong
      >? This permanently removes the vacancy and can't be undone.
    </p>

    <template #footer>
      <AppButton variant="ghost" :disabled="deleting" @click="emit('close')">Cancel</AppButton>
      <AppButton variant="danger-solid" :loading="deleting" @click="emit('confirm')">
        Delete vacancy
      </AppButton>
    </template>
  </AppDialog>
</template>

<style scoped>
.confirm__text {
  margin: 0;
  font-size: var(--text-base);
  color: var(--text);
  line-height: 1.6;
}

.confirm__text strong {
  font-weight: 600;
}
</style>
