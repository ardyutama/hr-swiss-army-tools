<script setup lang="ts">
import { useTemplateRef } from 'vue'
import type { ProblemMessageColor } from '@/shared/problem-details'
import type { FormLayoutColumn } from '../api'
import ColumnMappingEditor from './ColumnMappingEditor.vue'

/**
 * The layout edit flow's dialog: re-map the saved Form Layout against its
 * header snapshot. Dismissible like any editor — closing drops the draft
 * (the editor rebuilds it on the next opening).
 */
const open = defineModel<boolean>('open', { required: true })

withDefaults(
  defineProps<{
    /** The saved header snapshot the mapping is edited against. */
    headers: string[]
    /** The saved mapping to pre-fill from. */
    initialColumns: FormLayoutColumn[] | null
    busy?: boolean
    alert?: { color: ProblemMessageColor; title: string; description?: string } | null
  }>(),
  { busy: false, alert: null },
)

const emit = defineEmits<{
  save: [columns: FormLayoutColumn[]]
}>()

const editor = useTemplateRef<{
  submit: () => Promise<void> | undefined
  submitError: string | null
}>('editor')
</script>

<template>
  <UModal
    v-model:open="open"
    title="Form layout"
    description="Map this vacancy's Google Form export. Columns are matched by position, never by header text."
    :dismissible="!busy"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <ColumnMappingEditor
        ref="editor"
        :open="open"
        :headers="headers"
        :initial-columns="initialColumns"
        :busy="busy"
        :alert="alert"
        @save="emit('save', $event)"
      />
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
            @click="open = false"
          >
            Cancel
          </UButton>
          <UButton
            color="primary"
            :loading="busy"
            @click="editor?.submit()"
          >
            {{ busy ? 'Saving…' : 'Save layout' }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
