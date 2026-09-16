<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import ImportDropZone from './ImportDropZone.vue'
import { classifyImportFiles, type RejectedImportFile } from '../validation'

const open = defineModel<boolean>('open', { required: true })

withDefaults(
  defineProps<{
    busy?: boolean
    alert?: { color: 'error' | 'warning'; title: string; description?: string } | null
    summary?: string | null
  }>(),
  { busy: false, alert: null, summary: null },
)

const emit = defineEmits<{
  emlFiles: [files: File[]]
  csvFile: [file: File]
}>()

const rejected = shallowRef<RejectedImportFile[]>([])

function onFiles(files: File[]) {
  const dispatch = classifyImportFiles(files)
  if (dispatch.kind === 'rejected') {
    rejected.value = dispatch.rejected
    return
  }
  rejected.value = []
  if (dispatch.kind === 'eml') {
    emit('emlFiles', dispatch.files)
  } else {
    emit('csvFile', dispatch.file)
  }
}

watch(
  open,
  (isOpen) => {
    if (!isOpen) {
      rejected.value = []
    }
  },
  { flush: 'sync' },
)
</script>

<template>
  <UModal
    v-model:open="open"
    title="Import candidates"
    :dismissible="!busy"
  >
    <template #body>
      <div class="flex flex-col">
        <ImportDropZone :busy="busy" @files="onFiles" />
        <UAlert
          v-if="rejected.length > 0"
          color="error"
          variant="subtle"
          icon="i-lucide-file-x"
          title="Nothing was uploaded"
          role="alert"
          class="mt-3"
        >
          <template #description>
            <ul class="list-disc pl-4">
              <li v-for="file in rejected" :key="file.name">{{ file.name }} — {{ file.reason }}</li>
            </ul>
          </template>
        </UAlert>
        <UAlert
          v-if="summary"
          color="success"
          variant="subtle"
          icon="i-lucide-circle-check"
          :title="summary"
          role="status"
          class="mt-3"
        />
        <UAlert
          v-if="alert"
          :color="alert.color"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="alert.title"
          :description="alert.description"
          role="alert"
          class="mt-3"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton color="neutral" variant="outline" :disabled="busy" @click="open = false">
          {{ summary ? 'Close' : 'Cancel' }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
