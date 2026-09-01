<script setup lang="ts">
import { shallowRef, useTemplateRef } from 'vue'

const props = withDefaults(
  defineProps<{
    busy?: boolean
    disabled?: boolean
  }>(),
  { busy: false, disabled: false },
)

const emit = defineEmits<{
  files: [files: File[]]
}>()

const fileInput = useTemplateRef<HTMLInputElement>('fileInput')
const dragDepth = shallowRef(0)
const dragOver = shallowRef(false)

const inactive = () => props.disabled || props.busy

function openFilePicker() {
  if (inactive()) {
    return
  }
  fileInput.value?.click()
}

function onInputChange(event: Event) {
  const input = event.target as HTMLInputElement
  emitFiles(Array.from(input.files ?? []))
  // Reset so picking the same file again re-triggers the change event.
  input.value = ''
}

function onDragEnter(event: DragEvent) {
  if (inactive()) {
    return
  }
  if (!hasFiles(event)) {
    return
  }
  dragDepth.value += 1
  dragOver.value = true
}

function onDragLeave() {
  if (dragDepth.value > 0) {
    dragDepth.value -= 1
  }
  if (dragDepth.value === 0) {
    dragOver.value = false
  }
}

function onDrop(event: DragEvent) {
  dragDepth.value = 0
  dragOver.value = false
  if (inactive()) {
    return
  }
  emitFiles(Array.from(event.dataTransfer?.files ?? []))
}

function hasFiles(event: DragEvent): boolean {
  return Array.from(event.dataTransfer?.types ?? []).includes('Files')
}

function emitFiles(files: File[]) {
  if (files.length === 0) {
    return
  }
  emit('files', files)
}
</script>

<template>
  <div
    class="dropzone flex min-h-60 flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-accented bg-muted p-10 text-center transition-colors"
    :class="{
      'dropzone--active border-primary bg-primary/10': dragOver,
      'dropzone--busy border-solid': busy,
    }"
    role="region"
    aria-label="Import .eml files"
    @dragenter.prevent="onDragEnter"
    @dragover.prevent
    @dragleave.prevent="onDragLeave"
    @drop.prevent="onDrop"
  >
    <span
      class="dropzone__icon mb-2 inline-flex size-14 items-center justify-center rounded-xl bg-primary/10 text-primary"
      :class="{ 'bg-default': dragOver }"
      aria-hidden="true"
    >
      <UIcon name="i-lucide-upload" class="size-7" />
    </span>
    <p class="dropzone__title text-lg font-semibold text-highlighted">Drop eml export to import the data</p>
    <p class="dropzone__hint max-w-md text-sm text-muted">
      Each email becomes a candidate of this vacancy; PDF attachments are kept as CV documents.
    </p>
    <UButton
      color="neutral"
      variant="outline"
      :disabled="disabled"
      :loading="busy"
      @click="openFilePicker"
    >
      {{ busy ? 'Importing…' : 'Choose .eml files' }}
    </UButton>
    <input
      ref="fileInput"
      type="file"
      class="sr-only"
      accept=".eml"
      multiple
      :disabled="disabled || busy"
      aria-label="Choose .eml files"
      @change="onInputChange"
    />
  </div>
</template>
