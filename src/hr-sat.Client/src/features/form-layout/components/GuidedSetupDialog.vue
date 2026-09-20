<script setup lang="ts">
import { useTemplateRef } from 'vue'
import type { ProblemMessageColor } from '@/shared/problem-details'
import type { FormLayoutColumn } from '../api'
import ColumnMappingEditor from './ColumnMappingEditor.vue'

/**
 * The guided setup flow's dialog: a Form Response import refused with "layout
 * required" holds its file, and this panel maps its columns so saving
 * completes the import. It is non-dismissible — the only exits are Cancel
 * import (drops the held file) and Save.
 */
const open = defineModel<boolean>('open', { required: true })

withDefaults(
  defineProps<{
    /** The held file's header row. */
    headers: string[]
    /** Current mapping to pre-fill from (drift re-map); null on the very first setup. */
    initialColumns: FormLayoutColumn[] | null
    /** Saved snapshot used to name picked columns the file no longer has (drift re-map). */
    initialHeaders?: string[] | null
    fileName?: string | null
    busy?: boolean
    alert?: { color: ProblemMessageColor; title: string; description?: string } | null
  }>(),
  { initialHeaders: null, fileName: null, busy: false, alert: null },
)

const emit = defineEmits<{
  /** Save completes the held file's import with the mapping as the layout payload. */
  save: [columns: FormLayoutColumn[]]
  /** Cancel import drops the held file; nothing is saved. */
  cancel: []
}>()

const editor = useTemplateRef<{
  submit: () => Promise<void> | undefined
  submitError: string | null
}>('editor')
</script>

<template>
  <UModal
    v-model:open="open"
    title="Set up the form layout to finish importing"
    :dismissible="false"
    :close="false"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <ColumnMappingEditor
        ref="editor"
        :open="open"
        :headers="headers"
        :initial-columns="initialColumns"
        :initial-headers="initialHeaders"
        :busy="busy"
        :alert="alert"
        @save="emit('save', $event)"
      >
        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-file-up"
          :title="`Import of &quot;${fileName ?? 'the uploaded file'}&quot; is waiting.`"
          description="Bind Name and Contact email, then save to finish the import."
        />
      </ColumnMappingEditor>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-between gap-4">
        <p class="text-sm" :class="editor?.submitError ? 'text-error' : 'text-muted'" role="status">
          {{ editor?.submitError ?? 'Name and Contact email are required.' }}
        </p>
        <div class="flex shrink-0 gap-2">
          <UButton
            color="neutral"
            variant="outline"
            :disabled="busy"
            @click="emit('cancel')"
          >
            Cancel import
          </UButton>
          <UButton
            color="primary"
            :loading="busy"
            @click="editor?.submit()"
          >
            {{ busy ? 'Saving and importing…' : 'Save layout & import' }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
