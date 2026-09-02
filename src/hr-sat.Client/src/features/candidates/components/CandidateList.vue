<script setup lang="ts">
import { computed } from 'vue'
import type {
  CandidateExtractionStatus,
  CandidateReviewStatus,
  CandidateSummary,
} from '../api'
import { candidateDisplayName } from '../format'

const props = withDefaults(
  defineProps<{
    vacancyId: string
    candidates: CandidateSummary[]
    readonly?: boolean
  }>(),
  { readonly: false },
)

const emit = defineEmits<{
  remove: [candidate: CandidateSummary]
}>()

const reviewStatusLabels: Record<CandidateReviewStatus, string> = {
  new: 'New',
  flagged: 'Flagged',
  shortlisted: 'Shortlisted',
  rejected: 'Rejected',
}

const reviewStatusColors: Record<CandidateReviewStatus, 'success' | 'error' | 'neutral' | 'primary'> = {
  new: 'primary',
  flagged: 'neutral',
  shortlisted: 'success',
  rejected: 'error',
}

const extractionStatusLabels: Record<CandidateExtractionStatus, string> = {
  pending: 'Extraction pending',
  succeeded: 'Extraction complete',
  failed: 'Extraction failed',
}

const extractionStatusColors: Record<CandidateExtractionStatus, 'success' | 'error' | 'neutral'> = {
  pending: 'neutral',
  succeeded: 'success',
  failed: 'error',
}

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
const columnWidths = computed(() =>
  props.readonly
    ? ['34%', '19%', '30%', '17%']
    : ['30%', '17%', '26%', '14%', '4.5rem'],
)
</script>

<template>
  <div class="overflow-x-auto">
    <table class="ctable w-full min-w-[52rem] table-fixed border-collapse">
      <colgroup>
        <col v-for="width in columnWidths" :key="width" :style="{ width }" />
      </colgroup>
      <thead class="ctable__head">
        <tr>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Candidate</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Match</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Notes</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Status</th>
          <th v-if="!readonly" class="ctable__col ctable__col--actions border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">
            <span class="sr-only">Actions</span>
          </th>
        </tr>
      </thead>

      <tbody>
        <tr v-for="candidate in candidates" :key="candidate.id" class="crow group">
          <td class="ctable__col crow__name border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="flex min-w-0 flex-col items-start gap-1">
              <RouterLink
                :to="{ name: 'candidate-review', params: { id: props.vacancyId, candidateId: candidate.id } }"
                class="crow__name-text block max-w-full truncate rounded-sm text-sm font-semibold text-highlighted no-underline hover:underline"
              >
                {{ candidateDisplayName(candidate) }}
              </RouterLink>
              <UBadge
                :color="extractionStatusColors[candidate.extractionStatus]"
                variant="subtle"
                size="xs"
              >
                {{ extractionStatusLabels[candidate.extractionStatus] }}
              </UBadge>
            </div>
          </td>

          <td class="ctable__col border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="crow__match text-sm tabular-nums text-highlighted">
              {{ candidate.match.matchedRequirements }} / {{ candidate.match.totalRequirements }}
            </span>
          </td>

          <td class="ctable__col border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span v-if="candidate.notes" class="crow__notes block truncate text-xs text-muted">{{ candidate.notes }}</span>
            <span v-else class="crow__placeholder text-xs text-muted">—</span>
          </td>

          <td class="ctable__col border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <UBadge :color="reviewStatusColors[candidate.reviewStatus]" variant="subtle">
              {{ reviewStatusLabels[candidate.reviewStatus] }}
            </UBadge>
          </td>

          <td v-if="!readonly" class="ctable__col ctable__col--actions border-b border-default px-2 py-4 text-right align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="crow__actions inline-flex items-center gap-1">
              <UButton
                icon="i-lucide-send"
                color="neutral"
                variant="ghost"
                aria-label="Send email — available once email templates exist"
                title="Send email — available once email templates exist"
                disabled
              />
              <UButton
                icon="i-lucide-trash-2"
                color="error"
                variant="ghost"
                aria-label="Delete candidate"
                title="Delete candidate"
                @click="emit('remove', candidate)"
              />
            </div>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.crow:last-child .ctable__col {
  border-bottom: none;
}
</style>
