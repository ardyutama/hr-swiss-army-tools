<script setup lang="ts">
import { computed, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { FormErrorEvent, FormSubmitEvent } from '@nuxt/ui'
import type { ProblemMessageColor } from '@/shared/problem-details'
import type { FormLayoutColumn, FormLayoutRole } from '../api'
import RoleBindingFields from './RoleBindingFields.vue'
import DisplayColumnList from './DisplayColumnList.vue'
import {
  createFormLayoutSchema,
  formLayoutColumnsFromRows,
  formLayoutRows,
  type FormLayoutFormOutput,
  type FormLayoutRow,
} from '../validation'

const open = defineModel<boolean>('open', { required: true })

const props = withDefaults(
  defineProps<{
    /** 'edit' maps the saved snapshot; 'guided' maps the held file and saving completes its import. */
    mode: 'edit' | 'guided'
    /** The header row the mapping is edited against (snapshot in edit mode, the held file's headers in guided mode). */
    headers: string[]
    /** Current mapping to pre-fill from; null on the very first setup. */
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
  save: [columns: FormLayoutColumn[]]
  cancel: []
}>()

const form = reactive({ rows: [] as FormLayoutRow[] })
const submitError = shallowRef<string | null>(null)
// The footer lives outside the UForm body, so submit goes through the form ref.
const formRef = useTemplateRef<{ submit: () => Promise<void> }>('formRef')

const schema = computed(() => createFormLayoutSchema(props.headers.length))

const title = computed(() =>
  props.mode === 'guided' ? 'Set up the form layout to finish importing' : 'Form layout',
)
const submitLabel = computed(() => {
  if (props.busy) {
    return props.mode === 'guided' ? 'Saving and importing…' : 'Saving…'
  }
  return props.mode === 'guided' ? 'Save layout & import' : 'Save layout'
})
const cancelLabel = computed(() => (props.mode === 'guided' ? 'Cancel import' : 'Cancel'))

// Rebuild the draft each time the panel opens; the panel is stateless between
// openings. Default ('pre') flush, never 'sync': open is patched before the
// headers props in the parent template, and a sync watcher would read them
// stale (empty on the first guided import, the old snapshot on a drift re-map).
watch(open, (isOpen) => {
  if (isOpen) {
    form.rows = formLayoutRows(props.headers, props.initialColumns, props.initialHeaders ?? undefined)
    submitError.value = null
  }
})

function onBind(role: FormLayoutRole, ordinal: number | null) {
  const current = form.rows.find((row) => row.role === role)
  if (current && current.ordinal === ordinal) {
    return
  }
  if (current) {
    // Un-binding a role un-picks the column: the Column Label is discarded.
    current.role = null
    current.display = false
    current.label = ''
  }
  if (ordinal !== null) {
    const next = form.rows.find((row) => row.ordinal === ordinal)
    if (next) {
      next.role = role
      next.display = false
    }
  }
  submitError.value = null
}

function onToggle(ordinal: number, checked: boolean) {
  const row = form.rows.find((candidate) => candidate.ordinal === ordinal)
  if (!row) {
    return
  }
  row.display = checked
  if (!checked) {
    row.label = ''
  }
  submitError.value = null
}

function onUpdateLabel(ordinal: number, label: string) {
  const row = form.rows.find((candidate) => candidate.ordinal === ordinal)
  if (row) {
    row.label = label
  }
  submitError.value = null
}

function onSubmit(event: FormSubmitEvent<FormLayoutFormOutput>) {
  submitError.value = null
  emit('save', formLayoutColumnsFromRows(event.data.rows))
}

// UForm emits 'error' only when a submit fails validation — the footer hint is
// replaced by the first error until the mapping changes.
function onFormError(event: FormErrorEvent) {
  submitError.value = event.errors[0]?.message ?? 'Check the mapping and try again.'
}
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title"
    :description="
      props.mode === 'edit'
        ? 'Map this vacancy\'s Google Form export. Columns are matched by position, never by header text.'
        : undefined
    "
    :dismissible="props.mode !== 'guided' && !props.busy"
    :close="props.mode !== 'guided'"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <UForm
        ref="formRef"
        :schema="schema"
        :state="form"
        class="flex flex-col gap-5"
        @submit="onSubmit"
        @error="onFormError"
      >
        <UAlert
          v-if="props.mode === 'guided'"
          color="neutral"
          variant="subtle"
          icon="i-lucide-file-up"
          :title="`Import of &quot;${props.fileName ?? 'the uploaded file'}&quot; is waiting.`"
          description="Bind Name and Contact email, then save to finish the import."
        />

        <RoleBindingFields
          :rows="form.rows"
          :busy="props.busy"
          @bind="onBind"
          @update-label="onUpdateLabel"
        />

        <USeparator />

        <DisplayColumnList
          :rows="form.rows"
          :busy="props.busy"
          @toggle="onToggle"
          @update-label="onUpdateLabel"
        />

        <UAlert
          v-if="props.alert"
          :color="props.alert.color"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="props.alert.title"
          :description="props.alert.description"
          role="alert"
        />
      </UForm>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-between gap-4">
        <p class="text-sm" :class="submitError ? 'text-error' : 'text-muted'" role="status">
          {{ submitError ?? 'Name and Contact email are required.' }}
        </p>
        <div class="flex shrink-0 gap-2">
          <UButton
            color="neutral"
            variant="outline"
            :disabled="props.busy"
            @click="emit('cancel')"
          >
            {{ cancelLabel }}
          </UButton>
          <UButton
            color="primary"
            :loading="props.busy"
            @click="formRef?.submit()"
          >
            {{ submitLabel }}
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
