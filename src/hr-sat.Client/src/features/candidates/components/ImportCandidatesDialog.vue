<script setup lang="ts">
import ImportDropZone from './ImportDropZone.vue'

const open = defineModel<boolean>('open', { required: true })

withDefaults(
  defineProps<{
    busy?: boolean
    error?: string | null
  }>(),
  { busy: false, error: null },
)

const emit = defineEmits<{
  files: [files: File[]]
}>()
</script>

<template>
  <UModal
    v-model:open="open"
    title="Import .eml files"
    :dismissible="!busy"
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
        <UButton color="neutral" variant="outline" :disabled="busy" @click="open = false">
          Cancel
        </UButton>
      </div>
    </template>
  </UModal>
</template>
