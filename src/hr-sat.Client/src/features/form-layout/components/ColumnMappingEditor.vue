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

/**
 * The column-mapping editor shared by the flow's two dialogs (guided setup,
 * layout edit). It owns the draft rows, the input rules, and the submit
 * wiring; each dialog owns its frame, labels, and footer buttons — the
 * footer reaches submit and the first error through the exposed members.
 */
const props = withDefaults(
  defineProps<{
    /** Mirrors the dialog's open model: the draft rebuilds on every opening. */
    open: boolean
    /** The header row the mapping is edited against. */
    headers: string[]
    /** Current mapping to pre-fill from; null on the very first setup. */
    initialColumns: FormLayoutColumn[] | null
    /** Saved snapshot used to name picked columns the file no longer has (drift re-map). */
    initialHeaders?: string[] | null
    busy?: boolean
    alert?: { color: ProblemMessageColor; title: string; description?: string } | null
  }>(),
  { initialHeaders: null, busy: false, alert: null },
)

const emit = defineEmits<{
  save: [columns: FormLayoutColumn[]]
}>()

const form = reactive({ rows: [] as FormLayoutRow[] })
const submitError = shallowRef<string | null>(null)
// The footer lives outside the UForm body, so submit goes through the form ref.
const formRef = useTemplateRef<{ submit: () => Promise<void> }>('formRef')

const schema = computed(() => createFormLayoutSchema(props.headers.length))

// Rebuild the draft each time the panel opens; the panel is stateless between
// openings. Setup covers the fresh mount (UModal unmounts closed content);
// the watch covers content that stays mounted. Default ('pre') flush, never
// 'sync': open is patched before the headers props in the parent template,
// and a sync watcher would read them stale.
rebuild()
watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      rebuild()
    }
  },
)

function rebuild() {
  form.rows = formLayoutRows(
    props.headers,
    props.initialColumns,
    props.initialHeaders ?? undefined,
  )
  submitError.value = null
}

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

defineExpose({
  submit: () => formRef.value?.submit(),
  submitError,
})
</script>

<template>
  <UForm
    ref="formRef"
    :schema="schema"
    :state="form"
    class="flex flex-col gap-5"
    @submit="onSubmit"
    @error="onFormError"
  >
    <slot />

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
