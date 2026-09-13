<script setup lang="ts">
import { shallowRef, watch } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import type { EmailTemplate, EmailTemplateKind } from '../api'
import { emailTemplateKindLabel } from '../useEmailTemplates'
import { defaultPreviewCandidate, useTemplatePreview } from '../useTemplatePreview'
import PreparedMessagePreview from './PreparedMessagePreview.vue'

const open = defineModel<boolean>('open', { required: true })

const props = defineProps<{
  vacancyId: string
  kind: EmailTemplateKind
  template: EmailTemplate | null
  candidates: CandidateSummary[]
}>()

const selectedCandidateId = shallowRef<number | null>(null)
const { preview, previewing, previewError, renderNow, schedule, clear } = useTemplatePreview(
  () => props.vacancyId,
)

watch(open, (isOpen) => {
  clear()
  if (!isOpen) {
    return
  }
  selectedCandidateId.value = defaultPreviewCandidate(props.candidates, props.kind)?.id ?? null
  if (props.template && selectedCandidateId.value !== null) {
    renderNow({
      subject: props.template.subject,
      body: props.template.body,
      candidateId: selectedCandidateId.value,
    })
  }
})

watch(selectedCandidateId, (candidateId) => {
  if (!open.value || !props.template) {
    return
  }
  if (candidateId === null) {
    clear()
    return
  }
  schedule({
    subject: props.template.subject,
    body: props.template.body,
    candidateId,
  })
})
</script>

<template>
  <UModal
    v-model:open="open"
    :title="`${emailTemplateKindLabel(props.kind)} template`"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <div v-if="props.template" class="flex flex-col gap-5">
        <div class="flex flex-col gap-1">
          <p class="font-medium text-highlighted">{{ props.template.subject }}</p>
          <p class="whitespace-pre-line text-sm text-muted">{{ props.template.body }}</p>
        </div>

        <PreparedMessagePreview
          v-model:candidate-id="selectedCandidateId"
          :candidates="props.candidates"
          :draft-subject="props.template.subject"
          :draft-body="props.template.body"
          :preview="preview"
          :previewing="previewing"
          :error="previewError"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex justify-end">
        <UButton color="neutral" variant="outline" @click="open = false">Close</UButton>
      </div>
    </template>
  </UModal>
</template>
