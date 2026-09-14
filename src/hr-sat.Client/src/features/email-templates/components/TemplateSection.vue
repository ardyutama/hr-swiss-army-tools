<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue'
import { formatDate } from '@/features/vacancies/format'
import type { EmailTemplate, EmailTemplateKind, TemplateSource } from '../api'
import { emailTemplateKindLabel } from '../useEmailTemplates'

const props = defineProps<{
  kind: EmailTemplateKind
  template: EmailTemplate | null
  /** null while the sources fetch is still in flight. */
  sources: TemplateSource[] | null
  /** Closed vacancy: mutations hidden, View and copy-from stay (settled lifecycle rule). */
  readonly: boolean
  deleting: boolean
  deleteError: string | null
}>()

const emit = defineEmits<{
  create: []
  edit: []
  view: []
  confirmDelete: []
  copy: [source: TemplateSource]
}>()

const heading = computed(() => `${emailTemplateKindLabel(props.kind)} template`)
const emptySentence = computed(
  () => `No ${props.kind === 'shortlisted' ? 'shortlisted' : 'rejected'} template yet.`,
)

const confirmingDelete = shallowRef(false)

// A successful delete flips the template to null — leave the confirm state with it.
watch(
  () => props.template,
  () => {
    confirmingDelete.value = false
  },
)

function onCopyPick(event: Event) {
  const select = event.target as HTMLSelectElement
  const source = props.sources?.find((candidate) => String(candidate.vacancyId) === select.value)
  select.value = ''
  if (source) {
    emit('copy', source)
  }
}
</script>

<template>
  <section
    class="overflow-hidden rounded-xl border border-default bg-default"
    :aria-label="heading"
  >
    <h3 class="border-b border-default px-4 py-2.5 text-sm font-semibold text-highlighted">
      {{ heading }}
    </h3>

    <!-- Template exists: subject + excerpt, Edit is the primary action. -->
    <div v-if="props.template" class="flex flex-col gap-2 px-4 py-3">
      <p class="truncate font-medium text-highlighted">{{ props.template.subject }}</p>
      <p class="line-clamp-2 text-sm text-muted">{{ props.template.body }}</p>
      <div v-if="!confirmingDelete" class="mt-1 flex flex-wrap items-center gap-2">
        <UButton
          v-if="!props.readonly"
          size="sm"
          color="primary"
          variant="soft"
          icon="i-lucide-pencil"
          @click="emit('edit')"
        >
          Edit
        </UButton>
        <UButton
          size="sm"
          color="neutral"
          variant="outline"
          icon="i-lucide-eye"
          @click="emit('view')"
        >
          View
        </UButton>
        <UButton
          v-if="!props.readonly"
          size="sm"
          color="neutral"
          variant="ghost"
          icon="i-lucide-trash-2"
          @click="confirmingDelete = true"
        >
          Delete
        </UButton>
      </div>
    </div>

    <!-- Empty: one sentence plus one action (ADR-0008 #14). -->
    <div v-else class="flex flex-col gap-2 px-4 py-3">
      <p class="text-sm text-muted">{{ emptySentence }}</p>
      <div v-if="!props.readonly">
        <UButton
          size="sm"
          color="primary"
          variant="soft"
          icon="i-lucide-plus"
          @click="emit('create')"
        >
          Create template
        </UButton>
      </div>
    </div>

    <UAlert
      v-if="props.deleteError"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      :title="props.deleteError"
      role="alert"
      class="mx-4 mb-3"
    />

    <!-- Section footer: the delete confirm morphs over the copy-from picker. -->
    <div
      v-if="confirmingDelete || (props.sources !== null && props.sources.length > 0)"
      class="border-t border-default px-4 py-3"
    >
      <div v-if="confirmingDelete" class="flex flex-wrap items-center justify-between gap-3">
        <span class="text-sm font-medium text-highlighted">Delete this template?</span>
        <div class="flex items-center gap-2">
          <UButton
            size="sm"
            color="neutral"
            variant="outline"
            :disabled="props.deleting"
            @click="confirmingDelete = false"
          >
            Cancel
          </UButton>
          <UButton size="sm" color="error" :loading="props.deleting" @click="emit('confirmDelete')">
            Delete
          </UButton>
        </div>
      </div>

      <label v-else class="flex items-center gap-2">
        <span class="shrink-0 text-xs font-medium text-muted">Copy from a previous vacancy</span>
        <select
          class="min-h-10 min-w-0 flex-1 rounded-xl border border-default bg-default px-3 py-2 text-sm text-highlighted outline-none focus:border-primary"
          @change="onCopyPick"
        >
          <option value="" disabled selected>Choose a vacancy…</option>
          <option v-for="source in props.sources ?? []" :key="source.vacancyId" :value="source.vacancyId">
            {{ source.vacancyTitle }} — {{ formatDate(source.openedOn) }}
          </option>
        </select>
      </label>
    </div>
  </section>
</template>
