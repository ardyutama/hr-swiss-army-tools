<script setup lang="ts">
import { toRef, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@nuxt/ui/composables/useToast'
import ReviewHeader from '@/features/review/components/ReviewHeader.vue'
import RequirementsPanel from '@/features/review/components/RequirementsPanel.vue'
import CandidateDetailsPanel from '@/features/review/components/CandidateDetailsPanel.vue'
import SourceEmailPanel from '@/features/review/components/SourceEmailPanel.vue'
import NotesEditor from '@/features/review/components/NotesEditor.vue'
import CvViewer from '@/features/review/components/CvViewer.vue'
import ReviewActionBar from '@/features/review/components/ReviewActionBar.vue'
import { useReview } from '@/features/review/useReview'
import type { CandidateReviewStatus } from '@/features/candidates/api'
import type { CandidateDetailsPayload } from '@/features/review/api'

const props = defineProps<{
  id: string
  candidateId: string
}>()

const router = useRouter()
const toast = useToast()
const {
  vacancy,
  candidate,
  loadError,
  viewState,
  candidateDetailsWarning,
  position,
  total,
  previousCandidateId,
  nextCandidateId,
  notes,
  notesDirty,
  notesSaveState,
  notesSavedAt,
  savingDetails,
  detailsError,
  savingRequirementId,
  requirementError,
  deciding,
  decisionError,
  load,
  saveNotes,
  updateDetails,
  updateRequirementReview,
  decide,
  advanceToNextCandidate,
} = useReview(toRef(props, 'id'), toRef(props, 'candidateId'))

watch(candidateDetailsWarning, (warning) => {
  if (warning) {
    toast.add({
      title: warning,
      color: 'warning',
      class: 'review-details-warning-toast',
    })
  }
})

function goToCandidate(id: string | null) {
  if (id === null) {
    return
  }
  void router.push({ name: 'candidate-review', params: { id: props.id, candidateId: id } })
}

// ADR-0008 #9: Prev/Next navigation silently commits pending notes.
async function onPrev() {
  if (await saveNotes()) {
    goToCandidate(previousCandidateId.value)
  }
}

async function onNext() {
  goToCandidate(await advanceToNextCandidate())
}

// Decision-as-commit: one call saves notes, sets the status, and auto-advances.
async function onDecide(status: CandidateReviewStatus) {
  const next = await decide(status)
  goToCandidate(next)
}

function onDetailsSave(payload: CandidateDetailsPayload) {
  void updateDetails(payload)
}

function onRequirementToggle(requirementId: number, confirmed: boolean) {
  void updateRequirementReview(requirementId, confirmed)
}
</script>

<template>
  <div class="flex flex-col gap-5">
    <!-- Shape-matched skeleton (ADR-0008 #14) -->
    <div
      v-if="viewState === 'loading'"
      class="flex flex-col gap-5"
      aria-busy="true"
      aria-label="Loading review workspace"
    >
      <div class="flex items-center gap-3">
        <USkeleton class="size-10 rounded-xl" />
        <div class="min-w-0 flex-1">
          <USkeleton class="h-6 w-1/3" />
          <USkeleton class="mt-2 h-3 w-24" />
        </div>
        <USkeleton class="h-5 w-12" />
      </div>
      <div class="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <div class="flex flex-col gap-5">
          <USkeleton class="h-40 w-full rounded-xl" />
          <USkeleton class="h-56 w-full rounded-xl" />
          <USkeleton class="h-24 w-full rounded-xl" />
        </div>
        <USkeleton class="h-[32rem] w-full rounded-xl" />
      </div>
    </div>

    <UAlert
      v-else-if="viewState === 'error'"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      title="Couldn't load the review workspace"
      :description="loadError ?? undefined"
      role="alert"
      :actions="[{ label: 'Try again', color: 'error', variant: 'outline', onClick: load }]"
    />

    <template v-else-if="vacancy && candidate">
      <ReviewHeader
        :vacancy-id="id"
        :title="vacancy.title"
        :opened-on="vacancy.openedOn"
        :position="position"
        :total="total"
        :processed="vacancy.progress.processedCandidates"
        :progress-total="vacancy.progress.totalCandidates"
      />

      <div class="grid gap-5 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <!-- Left column scrolls independently; the viewer keeps a fixed height. -->
        <div
          class="flex min-w-0 flex-col gap-5 lg:max-h-[calc(100vh-14rem)] lg:overflow-y-auto lg:pr-1"
        >
          <RequirementsPanel
            :requirements="vacancy.requirements"
            :reviews="candidate.requirementReviews"
            :saving-requirement-id="savingRequirementId"
            :error="requirementError"
            @toggle="onRequirementToggle"
          />
          <CandidateDetailsPanel
            :candidate="candidate"
            :saving="savingDetails"
            :error="detailsError"
            @save="onDetailsSave"
          />
          <SourceEmailPanel
            :subject="candidate.sourceSubject"
            :sender-name="candidate.sourceSenderName"
            :sender-email="candidate.sourceSenderEmail"
            :sent-at="candidate.sourceSentAt"
            :body="candidate.sourceBodyText"
          />
          <NotesEditor
            v-model="notes"
            :save-state="notesSaveState"
            :saved-at="notesSavedAt"
            :dirty="notesDirty"
            @save="saveNotes"
          />
        </div>

        <CvViewer :documents="candidate.documents" class="self-start lg:sticky lg:top-6" />
      </div>

      <ReviewActionBar
        :review-status="candidate.reviewStatus"
        :can-prev="previousCandidateId !== null"
        :can-next="nextCandidateId !== null"
        :busy="deciding"
        :error="decisionError"
        @prev="onPrev"
        @next="onNext"
        @decide="onDecide"
      />
    </template>
  </div>
</template>

<style scoped>
:global(.review-details-warning-toast) {
  position: fixed !important;
  top: 1rem !important;
  right: 1rem !important;
  bottom: auto !important;
  left: auto !important;
  width: min(24rem, calc(100vw - 2rem)) !important;
  z-index: 101 !important;
}
</style>
