<script setup lang="ts">
import { computed, nextTick, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { FormSubmitEvent } from '@nuxt/ui'
import type { CandidateSummary } from '@/features/candidates/api'
import { fieldErrorsOf, formErrorsOnly } from '@/shared/validation'
import type { EmailTemplateKind, EmailTemplateWritePayload } from '../api'
import { defaultPreviewCandidate, useTemplatePreview } from '../useTemplatePreview'
import {
  SUBJECT_MAX_LENGTH,
  templateFormFieldKeys,
  templateFormSchema,
  templatePlaceholders,
  templateServerFieldNames,
  type TemplateFormOutput,
} from '../validation'
import PreparedMessagePreview from './PreparedMessagePreview.vue'

const open = defineModel<boolean>('open', { required: true })

const props = defineProps<{
  vacancyId: string
  kind: EmailTemplateKind
  /** True when the kind already owns a template (PUT still upserts; copy-from may prefill over it). */
  editing: boolean
  /** Existing text for Edit, source text for copy-from; null for a fresh create. */
  initial: EmailTemplateWritePayload | null
  /** Vacancy title when the editor opened from the copy-from picker; nothing persists until save. */
  copiedFrom: string | null
  /** Closed vacancy copy-from is inspectable but cannot mutate the vacancy. */
  readonly: boolean
  candidates: CandidateSummary[]
  saving: boolean
  /** Non-validation request failure, surfaced inline. */
  error: string | null
}>()

const emit = defineEmits<{
  submit: [payload: EmailTemplateWritePayload]
}>()

const title = computed(() =>
  props.readonly && props.copiedFrom
    ? `Template copied from ${props.copiedFrom}`
    : `${props.editing ? 'Edit' : 'Create'} ${props.kind} template`,
)
const submitLabel = computed(() => (props.editing ? 'Save changes' : 'Create template'))

const form = reactive({ subject: '', body: '' })
const formRef = useTemplateRef('formRef')

const selectedCandidateId = shallowRef<number | null>(null)
const { preview, previewing, previewError, renderNow, schedule, clear } = useTemplatePreview(
  () => props.vacancyId,
)

const subjectCounterClass = computed(() =>
  form.subject.length >= SUBJECT_MAX_LENGTH - 100 ? 'text-warning' : 'text-muted',
)

watch(open, (isOpen) => {
  clear()
  if (!isOpen) {
    return
  }
  form.subject = props.initial?.subject ?? ''
  form.body = props.initial?.body ?? ''
  selectedCandidateId.value =
    defaultPreviewCandidate(props.candidates, props.kind)?.id ?? null
  if (selectedCandidateId.value !== null) {
    // The opening preview fires immediately; later edits are debounced.
    renderNow({ subject: form.subject, body: form.body, candidateId: selectedCandidateId.value })
  }
})

watch([() => form.subject, () => form.body, selectedCandidateId], () => {
  if (!open.value) {
    return
  }
  if (selectedCandidateId.value === null) {
    clear()
    return
  }
  schedule({ subject: form.subject, body: form.body, candidateId: selectedCandidateId.value })
})

// Placeholder chips insert at the cursor of the last-focused text field
// (default: body). Refs wrap the fields so the native element lookup does not
// depend on Nuxt UI internals.
const subjectField = useTemplateRef<HTMLElement>('subjectField')
const bodyField = useTemplateRef<HTMLElement>('bodyField')
const lastFocusedField = shallowRef<'subject' | 'body'>('body')

function insertPlaceholder(token: string) {
  const field = lastFocusedField.value
  const root = field === 'subject' ? subjectField.value : bodyField.value
  const input = root?.querySelector('input, textarea') as
    | HTMLInputElement
    | HTMLTextAreaElement
    | null
  const current = form[field]
  const start = input?.selectionStart ?? current.length
  const end = input?.selectionEnd ?? current.length
  form[field] = `${current.slice(0, start)}${token}${current.slice(end)}`
  void nextTick(() => {
    input?.focus()
    input?.setSelectionRange(start + token.length, start + token.length)
  })
}

function onSubmit(event: FormSubmitEvent<TemplateFormOutput>) {
  if (props.readonly) {
    return
  }
  emit('submit', { subject: event.data.subject, body: event.data.body })
}

defineExpose({
  /** Routes a failed save's ValidationProblem back onto the fields it names. */
  applyServerErrors(error: unknown) {
    const serverErrors = fieldErrorsOf(error)
    if (!serverErrors) {
      return
    }
    const formErrors = formErrorsOnly(serverErrors, templateFormFieldKeys)
    formRef.value?.setErrors(
      Object.entries(formErrors).flatMap(([key, messages]) =>
        messages.map((message) => ({
          name: templateServerFieldNames[key] ?? key,
          message,
        })),
      ),
    )
  },
})
</script>

<template>
  <UModal
    v-model:open="open"
    :title="title"
    :dismissible="!props.saving"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <div class="flex flex-col gap-5">
        <p v-if="props.copiedFrom" class="flex items-center gap-1.5 text-xs text-muted">
          <UIcon name="i-lucide-copy" class="size-3.5 shrink-0" aria-hidden="true" />
          <span>Copied from {{ props.copiedFrom }} — nothing is saved until you save.</span>
        </p>

        <UAlert
          v-if="props.error"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="props.error"
          role="alert"
        />

        <UForm
          ref="formRef"
          :schema="templateFormSchema"
          :state="form"
          class="flex flex-col gap-5"
          @submit="onSubmit"
        >
          <div ref="subjectField" @focusin="lastFocusedField = 'subject'">
            <UFormField label="Subject" name="subject">
              <div class="flex w-full items-end gap-2">
                <UInput
                  v-model="form.subject"
                  :maxlength="SUBJECT_MAX_LENGTH"
                  :readonly="props.readonly"
                  autocomplete="off"
                  class="w-full"
                />
                <span
                  class="shrink-0 pb-2 text-xs tabular-nums"
                  :class="subjectCounterClass"
                  aria-hidden="true"
                >{{ form.subject.length }}/{{ SUBJECT_MAX_LENGTH }}</span>
              </div>
            </UFormField>
          </div>

          <div ref="bodyField" @focusin="lastFocusedField = 'body'">
            <UFormField label="Body" name="body">
              <UTextarea
                v-model="form.body"
                :rows="10"
                :readonly="props.readonly"
                class="w-full"
              />
            </UFormField>
          </div>

          <div v-if="!props.readonly" class="flex flex-wrap items-center gap-2">
            <span class="text-xs text-muted">Insert a placeholder:</span>
            <UButton
              v-for="placeholder in templatePlaceholders"
              :key="placeholder.token"
              size="xs"
              color="neutral"
              variant="soft"
              :aria-label="`Insert ${placeholder.label}`"
              @click="insertPlaceholder(placeholder.token)"
            >
              {{ placeholder.token }}
            </UButton>
          </div>
        </UForm>

        <PreparedMessagePreview
          v-model:candidate-id="selectedCandidateId"
          :candidates="props.candidates"
          :draft-subject="form.subject"
          :draft-body="form.body"
          :preview="preview"
          :previewing="previewing"
          :error="previewError"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end gap-2">
        <UButton color="neutral" variant="outline" :disabled="props.saving" @click="open = false">
          Cancel
        </UButton>
        <UButton
          v-if="!props.readonly"
          color="primary"
          :loading="props.saving"
          @click="formRef?.submit()"
        >
          {{ submitLabel }}
        </UButton>
      </div>
    </template>
  </UModal>
</template>
