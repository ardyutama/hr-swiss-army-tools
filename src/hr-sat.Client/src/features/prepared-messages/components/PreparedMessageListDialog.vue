<script setup lang="ts">
import { computed } from 'vue'
import type { CandidateSummary } from '@/features/candidates/api'
import { candidateDisplayName } from '@/features/candidates/format'
import type { RenderedMessage } from '@/features/email-templates/api'
import type { EmailTemplateKind } from '@/features/email-templates/api'
import { emailTemplateKindLabel } from '@/features/email-templates/format'
import { exclusionSummary, mailtoHref, scopeDescription } from '../format'
import type { PreparedMessageRow, PreparedMessagesViewState } from '../usePreparedMessages'

const open = defineModel<boolean>('open', { required: true })

const props = defineProps<{
  candidateCount: number
  roundName: string
  viewState: PreparedMessagesViewState
  loadError: string | null
  rows: PreparedMessageRow[]
  contactableCount: number
  missingEmail: CandidateSummary[]
  excludedUndecided: number
  excludedOutcome: number
  missingTemplateKinds: EmailTemplateKind[]
  copiedCandidateId: number | null
}>()

const emit = defineEmits<{
  'edit-templates': []
  'open-review': [candidate: CandidateSummary]
  retry: [candidate: CandidateSummary]
  copy: [candidate: CandidateSummary, message: RenderedMessage]
  reload: []
}>()

const description = computed(() => scopeDescription(props.candidateCount, props.roundName))

const exclusion = computed(() =>
  exclusionSummary(
    props.contactableCount,
    props.candidateCount,
    props.excludedUndecided,
    props.excludedOutcome,
  ),
)

const showEmpty = computed(
  () =>
    props.rows.length === 0 &&
    props.missingEmail.length === 0 &&
    props.missingTemplateKinds.length === 0,
)
</script>

<template>
  <UModal
    v-model:open="open"
    title="Prepared messages"
    :description="description"
    :ui="{ content: 'sm:max-w-2xl' }"
  >
    <template #body>
      <div class="flex flex-col gap-4">
        <div
          v-if="viewState === 'loading'"
          class="flex flex-col gap-3"
          aria-busy="true"
          aria-label="Loading prepared messages"
        >
          <USkeleton class="h-4 w-2/5" />
          <USkeleton class="h-24 w-full rounded-xl" />
          <USkeleton class="h-24 w-full rounded-xl" />
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
          <p v-if="exclusion" class="text-sm text-muted">{{ exclusion }}</p>

          <UAlert
            v-for="kind in missingTemplateKinds"
            :key="kind"
            color="warning"
            variant="subtle"
            icon="i-lucide-file-warning"
            :title="`No ${emailTemplateKindLabel(kind).toLowerCase()} template yet`"
            description="Create it to prepare these messages."
            :actions="[
              {
                label: 'Create template',
                color: 'warning',
                variant: 'outline',
                onClick: () => emit('edit-templates'),
              },
            ]"
          />

          <section
            v-if="missingEmail.length > 0"
            aria-label="Needs an email address"
            class="flex flex-col gap-2"
          >
            <h3 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
              Needs an email address
            </h3>
            <ul class="flex flex-col gap-1">
              <li
                v-for="candidate in missingEmail"
                :key="candidate.id"
                class="flex items-center justify-between gap-3"
              >
                <span class="text-sm text-highlighted">{{ candidateDisplayName(candidate) }}</span>
                <UButton
                  color="neutral"
                  variant="link"
                  size="sm"
                  @click="emit('open-review', candidate)"
                >
                  Add email in review
                </UButton>
              </li>
            </ul>
          </section>

          <UEmpty
            v-if="showEmpty"
            icon="i-lucide-mail-x"
            title="No candidates to contact yet"
            description="Contactable candidates are shortlisted or rejected and have an email address recorded."
            :actions="[
              { label: 'Edit templates', icon: 'i-lucide-pencil', onClick: () => emit('edit-templates') },
            ]"
          />

          <ul v-if="rows.length > 0" class="flex flex-col gap-3">
            <li
              v-for="row in rows"
              :key="row.candidate.id"
              class="flex flex-col gap-2 rounded-xl border border-default p-4"
            >
              <div class="flex items-baseline justify-between gap-3">
                <p class="text-sm font-semibold text-highlighted">
                  {{ candidateDisplayName(row.candidate) }}
                </p>
                <p class="text-xs text-muted">{{ row.candidate.contactEmail }}</p>
              </div>

              <div
                v-if="row.state.kind === 'loading'"
                class="flex flex-col gap-2"
                aria-busy="true"
                aria-label="Preparing message"
              >
                <USkeleton class="h-4 w-2/3" />
                <USkeleton class="h-12 w-full" />
              </div>

              <UAlert
                v-if="row.state.kind === 'error'"
                color="error"
                variant="subtle"
                icon="i-lucide-triangle-alert"
                title="Couldn't prepare this message"
                :description="row.state.message"
                :actions="[
                  {
                    label: 'Try again',
                    color: 'error',
                    variant: 'outline',
                    onClick: () => emit('retry', row.candidate),
                  },
                ]"
              />

              <template v-if="row.state.kind === 'ready'">
                <p class="text-sm font-medium text-highlighted">{{ row.state.message.subject }}</p>
                <p class="whitespace-pre-line text-sm text-muted">{{ row.state.message.body }}</p>
                <div class="flex flex-wrap gap-2">
                  <a
                    class="inline-flex items-center gap-1.5 rounded-md bg-primary/10 px-3 py-1.5 text-sm font-medium text-primary transition-colors hover:bg-primary/20 focus-visible:outline-2 focus-visible:outline-primary"
                    :href="
                      mailtoHref(
                        row.candidate.contactEmail ?? '',
                        row.state.message.subject,
                        row.state.message.body,
                      )
                    "
                  >
                    <UIcon name="i-lucide-send" class="size-4" aria-hidden="true" />
                    Open in mail client
                  </a>
                  <UButton
                    color="neutral"
                    variant="outline"
                    :icon="copiedCandidateId === row.candidate.id ? 'i-lucide-check' : 'i-lucide-copy'"
                    @click="emit('copy', row.candidate, row.state.message)"
                  >
                    {{ copiedCandidateId === row.candidate.id ? 'Copied' : 'Copy' }}
                  </UButton>
                </div>
              </template>
            </li>
          </ul>
        </template>
      </div>
    </template>

    <template #footer>
      <div class="flex justify-between gap-3">
        <UButton color="neutral" variant="outline" icon="i-lucide-pencil" @click="emit('edit-templates')">
          Edit templates
        </UButton>
        <UButton color="neutral" variant="outline" @click="open = false">Close</UButton>
      </div>
    </template>
  </UModal>
</template>
