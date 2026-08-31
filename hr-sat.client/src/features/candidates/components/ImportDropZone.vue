<script setup lang="ts">
import { shallowRef, useTemplateRef } from 'vue'
import AppButton from '@/shared/ui/AppButton.vue'
import AppIcon from '@/shared/ui/AppIcon.vue'

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
    class="dropzone"
    :class="{ 'dropzone--active': dragOver, 'dropzone--busy': busy }"
    role="region"
    aria-label="Import .eml files"
    @dragenter.prevent="onDragEnter"
    @dragover.prevent
    @dragleave.prevent="onDragLeave"
    @drop.prevent="onDrop"
  >
    <span class="dropzone__icon" aria-hidden="true">
      <AppIcon name="upload" :size="26" />
    </span>
    <p class="dropzone__title">Drop eml export to import the data</p>
    <p class="dropzone__hint">
      Each email becomes a candidate of this vacancy; PDF attachments are kept as CV documents.
    </p>
    <AppButton variant="ghost" :disabled="disabled" :loading="busy" @click="openFilePicker">
      {{ busy ? 'Importing…' : 'Choose .eml files' }}
    </AppButton>
    <input
      ref="fileInput"
      type="file"
      class="dropzone__input"
      accept=".eml"
      multiple
      :disabled="disabled || busy"
      aria-label="Choose .eml files"
      @change="onInputChange"
    />
  </div>
</template>

<style scoped>
.dropzone {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  text-align: center;
  padding: var(--space-10) var(--space-6);
  min-height: 15rem;
  background: var(--surface-subtle);
  border: 2px dashed var(--border-strong);
  border-radius: var(--radius);
  transition: border-color 0.15s ease, background 0.15s ease;
}

.dropzone--active {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.dropzone--busy {
  border-style: solid;
}

.dropzone__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 3.5rem;
  height: 3.5rem;
  border-radius: var(--radius);
  background: var(--accent-soft);
  color: var(--accent);
  margin-bottom: var(--space-2);
}

.dropzone--active .dropzone__icon {
  background: var(--surface);
}

.dropzone__title {
  margin: 0;
  font-size: var(--text-lg);
  font-weight: 600;
  color: var(--text);
}

.dropzone__hint {
  margin: 0;
  color: var(--muted);
  font-size: var(--text-sm);
  max-width: 30rem;
}

.dropzone .btn {
  margin-top: var(--space-3);
}

.dropzone__input {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
