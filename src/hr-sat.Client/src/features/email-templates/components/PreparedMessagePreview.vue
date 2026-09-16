<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { candidateDisplayName } from '@/features/candidates/format'
import type { RenderedMessage } from '../api'

const candidateId = defineModel<number | null>('candidateId', { required: true })
const candidateItems = computed(() =>
  props.candidates.map((candidate) => ({
    label: candidateDisplayName(candidate),
    value: candidate.id,
  })),
)

function onCandidatePick(candidateIdValue: number | undefined) {
  candidateId.value = candidateIdValue ?? null
}

const props = defineProps<{
  candidates: CandidateSummary[]
  /** Unrendered draft text, shown as the fallback when the round has no candidates. */
  draftSubject: string
  draftBody: string
  preview: RenderedMessage | null
  previewing: boolean
  error: string | null
}>()
</script>

<template>
  <section
    class="flex flex-col gap-3 rounded-xl border border-default bg-muted/30 p-3"
    aria-label="Prepared message"
  >
    <div class="flex items-center justify-between gap-3">
      <h4 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
        Prepared message
      </h4>
      <UIcon
        v-if="props.previewing"
        name="i-lucide-loader-circle"
        class="size-4 animate-spin text-muted"
        aria-hidden="true"
      />
    </div>

    <template v-if="props.candidates.length > 0">
      <label class="flex flex-col gap-1.5 text-sm">
        <span class="text-xs font-medium text-muted">Preview for</span>
        <USelect
          :model-value="candidateId ?? undefined"
          :items="candidateItems"
          value-key="value"
          label-key="label"
          aria-label="Preview for"
          class="w-full"
          @update:model-value="onCandidatePick"
        />
      </label>

      <UAlert
        v-if="props.error"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="props.error"
        role="alert"
      />
      <div v-else-if="props.preview" class="flex flex-col gap-1 rounded-lg bg-default px-3 py-2">
        <p class="text-sm font-medium text-highlighted">{{ props.preview.subject }}</p>
        <p class="whitespace-pre-line text-sm text-muted">{{ props.preview.body }}</p>
      </div>
    </template>

    <!-- No fake candidates: the draft passes through unrendered with a note. -->
    <template v-else>
      <p class="text-xs text-muted">Import candidates to preview a prepared message.</p>
      <div class="flex flex-col gap-1 rounded-lg bg-default px-3 py-2">
        <p class="text-sm font-medium text-highlighted">{{ props.draftSubject }}</p>
        <p class="whitespace-pre-line text-sm text-muted">{{ props.draftBody }}</p>
      </div>
    </template>
  </section>
</template>
