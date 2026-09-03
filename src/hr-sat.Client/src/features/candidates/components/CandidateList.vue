<script setup lang="ts">
import type {
  CandidateReviewStatus,
  CandidateSummary,
} from '../api'
import type { ReceivedSort } from '../filter'
import { candidateDisplayName, formatReceivedAt } from '../format'

withDefaults(
  defineProps<{
    candidates: CandidateSummary[]
    receivedSort: ReceivedSort
    readonly?: boolean
  }>(),
  { readonly: false },
)

const emit = defineEmits<{
  remove: [candidate: CandidateSummary]
  review: [candidate: CandidateSummary]
  toggleReceivedSort: []
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

// Column proportions applied via <colgroup> so the semantic table keeps a fixed layout.
// Order is fixed by the S3 sketch: Candidate | Received | CV | Notes | Review status | Actions.
const columnWidths = ['24%', '14%', '8%', '28%', '12%', '7.5rem']
</script>

<template>
  <div class="overflow-x-auto">
    <table class="ctable w-full min-w-[56rem] table-fixed border-collapse">
      <colgroup>
        <col v-for="width in columnWidths" :key="width" :style="{ width }" />
      </colgroup>
      <thead class="ctable__head">
        <tr>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Candidate</th>
          <th
            class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5"
            scope="col"
            :aria-sort="receivedSort === 'oldest' ? 'ascending' : 'descending'"
          >
            <button
              type="button"
              class="-mx-1 -my-0.5 inline-flex cursor-pointer items-center gap-1 rounded-md px-1 py-0.5 uppercase tracking-[0.06em] transition-colors hover:text-highlighted focus-visible:outline-2 focus-visible:outline-primary"
              :title="receivedSort === 'oldest' ? 'Oldest first - click for newest first' : 'Newest first - click for oldest first'"
              @click="emit('toggleReceivedSort')"
            >
              Received
              <UIcon
                :name="receivedSort === 'oldest' ? 'i-lucide-arrow-up' : 'i-lucide-arrow-down'"
                class="size-3.5"
                aria-hidden="true"
              />
            </button>
          </th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">CV</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Notes</th>
          <th class="ctable__col border-b border-default px-2 pb-3 pt-2 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">Review status</th>
          <th class="ctable__col ctable__col--actions border-b border-default px-2 pb-3 pt-0 text-left text-xs font-semibold uppercase tracking-[0.06em] text-muted first:pl-5 last:pr-5" scope="col">
            <span class="sr-only">Actions</span>
          </th>
        </tr>
      </thead>

      <tbody>
        <tr
          v-for="candidate in candidates"
          :key="candidate.id"
          class="crow group cursor-pointer focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-primary"
          tabindex="0"
          :aria-label="`Open ${candidateDisplayName(candidate)} for review`"
          @click="emit('review', candidate)"
          @keydown.enter.prevent="emit('review', candidate)"
          @keydown.space.prevent="emit('review', candidate)"
        >
          <td class="ctable__col crow__name border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="crow__name-text block max-w-full truncate text-sm font-semibold text-highlighted">{{ candidateDisplayName(candidate) }}</span>
          </td>

          <td class="ctable__col crow__received border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span class="block truncate text-xs tabular-nums text-muted">{{ formatReceivedAt(candidate.sourceSentAt) }}</span>
          </td>

          <td class="ctable__col crow__cv border-b border-default px-2 py-4 align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <span
              v-if="candidate.cvDocumentCount > 0"
              class="inline-flex items-center text-muted"
              :title="candidate.cvDocumentCount === 1 ? '1 CV document' : `${candidate.cvDocumentCount} CV documents`"
            >
              <UIcon name="i-lucide-paperclip" class="size-4" aria-hidden="true" />
              <span class="sr-only">{{ candidate.cvDocumentCount === 1 ? '1 CV document' : `${candidate.cvDocumentCount} CV documents` }}</span>
            </span>
            <UBadge v-else color="warning" variant="subtle" icon="i-lucide-file-warning">No CV</UBadge>
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

          <td class="ctable__col ctable__col--actions border-b border-default px-2 py-4 text-right align-middle transition-colors first:pl-5 last:pr-5 group-hover:bg-muted">
            <div class="crow__actions inline-flex items-center gap-1">
              <template v-if="!readonly">
                <UButton
                  icon="i-lucide-send"
                  color="neutral"
                  variant="ghost"
                  aria-label="Send email - available once email templates exist"
                  title="Send email - available once email templates exist"
                  disabled
                  @click.stop
                />
                <UButton
                  icon="i-lucide-trash-2"
                  color="error"
                  variant="ghost"
                  aria-label="Delete candidate"
                  title="Delete candidate"
                  @click.stop="emit('remove', candidate)"
                />
              </template>
              <UIcon
                name="i-lucide-chevron-right"
                class="crow__chevron size-4 shrink-0 text-muted transition-transform group-hover:translate-x-0.5 group-hover:text-highlighted"
                aria-hidden="true"
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
