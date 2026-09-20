<script setup lang="ts">
import { computed, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { formatDate } from '@/features/vacancies/format'
import { problemMessage, problemMessageText } from '@/shared/problem-details'
import { fieldErrorsOf } from '@/shared/validation'
import type {
  EmailTemplateKind,
  EmailTemplateWritePayload,
  TemplateSource,
  VacancyEmailTemplates,
} from '../api'
import type { EmailTemplatesViewState } from '../useEmailTemplates'
import TemplateEditorDialog from './TemplateEditorDialog.vue'
import TemplateSection from './TemplateSection.vue'
import TemplateViewDialog from './TemplateViewDialog.vue'

const open = defineModel<boolean>('open', { required: true })

// Presentation over flow-constructed lifecycles: the page's flow owns
// useEmailTemplates (sources merged in) and passes its state down; save/remove
// go up as emits and settle back through the exposed handles (the canonical
// form-dialog seam), so failed saves stay routed into the editor. This dialog
// keeps the editor/view mediation (nested dialogs, per-section delete confirm)
// and triggers the open-time loads via emits.
const props = defineProps<{
  vacancyId: string
  vacancyTitle: string
  openedOn: string
  status: 'open' | 'closed'
  /** The viewed round's unfiltered candidates — page filters must not shrink preview choices. */
  candidates: CandidateSummary[]
  templates: VacancyEmailTemplates | null
  loadError: string | null
  viewState: EmailTemplatesViewState
  saving: boolean
  deleting: boolean
  sources: Partial<Record<EmailTemplateKind, TemplateSource[]>>
}>()

const emit = defineEmits<{
  reload: []
  'load-sources': [kind: EmailTemplateKind]
  save: [kind: EmailTemplateKind, payload: EmailTemplateWritePayload]
  remove: [kind: EmailTemplateKind]
}>()

const closed = computed(() => props.status === 'closed')
const description = computed(() => `${props.vacancyTitle} · Opened ${formatDate(props.openedOn)}`)

watch(open, (isOpen) => {
  if (isOpen) {
    emit('reload')
    emit('load-sources', 'shortlisted')
    emit('load-sources', 'rejected')
  }
})

// Editor mediation: the section emits intent, the shell owns the editor dialog
// and routes failed saves back into it (field errors via the imperative handle,
// anything else as inline text).
const editorRef = useTemplateRef('editorRef')
const editorOpen = shallowRef(false)
const editorKind = shallowRef<EmailTemplateKind>('shortlisted')
const editorEditing = shallowRef(false)
const editorInitial = shallowRef<EmailTemplateWritePayload | null>(null)
const editorCopiedFrom = shallowRef<string | null>(null)
const editorReadonly = shallowRef(false)
const editorError = shallowRef<string | null>(null)

function openEditor(
  kind: EmailTemplateKind,
  initial: EmailTemplateWritePayload | null,
  copiedFrom: string | null,
  readonly = false,
) {
  editorKind.value = kind
  editorEditing.value = props.templates?.[kind] != null
  editorInitial.value = initial
  editorCopiedFrom.value = copiedFrom
  editorReadonly.value = readonly
  editorError.value = null
  editorOpen.value = true
}

function openCreate(kind: EmailTemplateKind) {
  openEditor(kind, null, null)
}

function openEdit(kind: EmailTemplateKind) {
  const template = props.templates?.[kind]
  openEditor(kind, template ? { subject: template.subject, body: template.body } : null, null)
}

/** Copy-from is an editor prefill, not a silent overwrite: nothing persists until HR saves. */
function openCopy(kind: EmailTemplateKind, source: TemplateSource) {
  openEditor(
    kind,
    { subject: source.subject, body: source.body },
    source.vacancyTitle,
    closed.value,
  )
}

function onEditorSubmit(payload: EmailTemplateWritePayload) {
  emit('save', editorKind.value, payload)
}

// Inline delete confirm: the section footer morphs; failures stay visible in
// the section that failed.
const deletingKind = shallowRef<EmailTemplateKind | null>(null)
const deleteErrors = reactive<Record<EmailTemplateKind, string | null>>({
  shortlisted: null,
  rejected: null,
})

function onDelete(kind: EmailTemplateKind) {
  deletingKind.value = kind
  deleteErrors[kind] = null
  emit('remove', kind)
}

const viewOpen = shallowRef(false)
const viewKind = shallowRef<EmailTemplateKind>('shortlisted')

function openView(kind: EmailTemplateKind) {
  viewKind.value = kind
  viewOpen.value = true
}

// The emitted save/remove settle back through these handles — the parent owns
// the try/catch (canonical form-dialog seam): a landed save closes the editor;
// a failed one routes named field errors into it, anything else as inline text.
defineExpose({
  saveSucceeded() {
    editorOpen.value = false
  },
  saveFailed(error: unknown) {
    if (fieldErrorsOf(error)) {
      editorRef.value?.applyServerErrors(error)
    } else {
      editorError.value = problemMessageText(problemMessage(error, "Couldn't save the template"))
    }
  },
  removeSettled(kind: EmailTemplateKind, error?: unknown) {
    if (error) {
      deleteErrors[kind] = problemMessageText(problemMessage(error, "Couldn't delete the template"))
    }
    deletingKind.value = null
  },
})
</script>

<template>
  <UModal v-model:open="open" title="Email templates" :description="description">
    <template #body>
      <div class="flex flex-col gap-4">
        <UAlert
          v-if="closed"
          color="warning"
          variant="subtle"
          icon="i-lucide-lock-keyhole"
          title="Vacancy closed — templates are settled. Reopen the vacancy to change them."
        />

        <!-- Shape-matched skeletons: two sections (ADR-0008 #14). -->
        <div
          v-if="viewState === 'loading'"
          class="flex flex-col gap-4"
          aria-busy="true"
          aria-label="Loading email templates"
        >
          <USkeleton class="h-28 w-full rounded-xl" />
          <USkeleton class="h-28 w-full rounded-xl" />
        </div>

        <UAlert
          v-else-if="viewState === 'error'"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Couldn't load email templates"
          :description="loadError ?? undefined"
          role="alert"
          :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: () => emit('reload') }]"
        />

        <template v-else>
          <TemplateSection
            kind="shortlisted"
            :template="templates?.shortlisted ?? null"
            :sources="sources.shortlisted ?? null"
            :readonly="closed"
            :deleting="deleting && deletingKind === 'shortlisted'"
            :delete-error="deleteErrors.shortlisted"
            @create="openCreate('shortlisted')"
            @edit="openEdit('shortlisted')"
            @view="openView('shortlisted')"
            @confirm-delete="onDelete('shortlisted')"
            @copy="(source) => openCopy('shortlisted', source)"
          />
          <TemplateSection
            kind="rejected"
            :template="templates?.rejected ?? null"
            :sources="sources.rejected ?? null"
            :readonly="closed"
            :deleting="deleting && deletingKind === 'rejected'"
            :delete-error="deleteErrors.rejected"
            @create="openCreate('rejected')"
            @edit="openEdit('rejected')"
            @view="openView('rejected')"
            @confirm-delete="onDelete('rejected')"
            @copy="(source) => openCopy('rejected', source)"
          />
        </template>
      </div>
    </template>
  </UModal>

  <TemplateEditorDialog
    ref="editorRef"
    v-model:open="editorOpen"
    :vacancy-id="props.vacancyId"
    :kind="editorKind"
    :editing="editorEditing"
    :initial="editorInitial"
    :copied-from="editorCopiedFrom"
    :readonly="editorReadonly"
    :candidates="props.candidates"
    :saving="saving"
    :error="editorError"
    @submit="onEditorSubmit"
  />
  <TemplateViewDialog
    v-model:open="viewOpen"
    :vacancy-id="props.vacancyId"
    :kind="viewKind"
    :template="templates?.[viewKind] ?? null"
    :candidates="props.candidates"
  />
</template>
