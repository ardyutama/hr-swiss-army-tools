<script setup lang="ts">
import AppDialog from '@/shared/ui/AppDialog.vue'
import ImportDropZone from './ImportDropZone.vue'

withDefaults(
  defineProps<{
    open: boolean
    busy?: boolean
    error?: string | null
  }>(),
  { busy: false, error: null },
)

const emit = defineEmits<{
  close: []
  files: [files: File[]]
}>()
</script>

<template>
  <AppDialog :open="open" title="Import .eml files" @close="emit('close')">
    <div class="import-dialog">
      <ImportDropZone :busy="busy" @files="(files) => emit('files', files)" />
      <p v-if="error" class="import-dialog__error" role="alert">{{ error }}</p>
    </div>
  </AppDialog>
</template>

<style scoped>
.import-dialog {
  display: flex;
  flex-direction: column;
}

.import-dialog__error {
  margin: var(--space-3) 0 0;
  padding: var(--space-3);
  border-radius: var(--radius-sm);
  background: var(--danger-soft);
  color: var(--danger);
  font-size: var(--text-sm);
  font-weight: 500;
}
</style>
