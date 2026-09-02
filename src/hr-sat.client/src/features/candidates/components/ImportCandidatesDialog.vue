<script setup lang="ts">
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
  <UModal
    :open="open"
    title="Import .eml files"
    :dismissible="!busy"
    @update:open="(value) => { if (!value) emit('close') }"
  >
    <template #body>
      <div class="flex flex-col">
        <ImportDropZone :busy="busy" @files="(files) => emit('files', files)" />
        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="error"
          role="alert"
          class="mt-3"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton color="neutral" variant="outline" :disabled="busy" @click="emit('close')">
          Cancel
        </UButton>
      </div>
    </template>
  </UModal>
</template>
