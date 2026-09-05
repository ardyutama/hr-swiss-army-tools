<script setup lang="ts">
import { computed, shallowRef, watch } from 'vue'
import { formatDate } from '@/features/vacancies/format'

const props = defineProps<{
  subject: string | null
  senderName: string | null
  senderEmail: string | null
  sentAt: string | null
  body: string | null
}>()

const open = shallowRef(false)

// A new candidate's email starts collapsed again.
watch(
  () => [props.subject, props.senderName, props.senderEmail, props.sentAt, props.body],
  () => {
    open.value = false
  },
)

const displaySubject = computed(() => props.subject?.trim() || '(no subject)')

const senderLabel = computed(
  () => props.senderName?.trim() || props.senderEmail?.trim() || 'Unknown sender',
)

const senderEmailLabel = computed(() => {
  const email = props.senderEmail?.trim()
  return props.senderName?.trim() && email ? email : null
})

const sentAtLabel = computed(() => (props.sentAt ? formatDate(props.sentAt) : null))

const displayBody = computed(
  () => (props.body?.trim() ? props.body : 'This email has no body text.'),
)
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default shadow-sm"
    aria-label="Source email"
  >
    <div class="flex items-center gap-3 px-4 py-3">
      <div class="min-w-0 flex-1">
        <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
          <h2 class="text-xs font-semibold uppercase tracking-[0.06em] text-muted">
            Source email
          </h2>
          <span v-if="sentAtLabel" class="text-xs text-muted">Received {{ sentAtLabel }}</span>
        </div>
        <p class="mt-0.5 truncate text-sm font-semibold text-highlighted">{{ displaySubject }}</p>
        <p class="truncate text-xs text-muted">From {{ senderLabel }}</p>
      </div>
      <UButton
        color="neutral"
        variant="ghost"
        size="sm"
        icon="i-lucide-mail-open"
        :aria-label="`Open source email: ${displaySubject}`"
        :aria-expanded="open"
        aria-haspopup="dialog"
        @click="open = true"
      >
        Open
      </UButton>
    </div>
  </section>

  <UModal
    :open="open"
    title="Source email"
    description="Original message retained from the candidate submission."
    scrollable
    :ui="{
      content: 'sm:max-w-2xl',
      body: 'p-0 sm:p-0',
      footer: 'border-t border-default bg-muted/30',
    }"
    @update:open="open = $event"
  >
    <template #body>
      <div class="flex flex-col gap-5 p-4 sm:p-6">
        <header>
          <h3 class="break-words text-base font-semibold leading-6 text-highlighted">
            {{ displaySubject }}
          </h3>
          <dl class="mt-3 flex flex-col gap-2 border-t border-default pt-3 text-sm">
            <div class="flex items-start gap-3">
              <dt class="w-16 shrink-0 text-xs font-semibold uppercase tracking-[0.06em] text-muted">
                From
              </dt>
              <dd class="m-0 min-w-0 break-words text-highlighted">
                {{ senderLabel }}
                <span v-if="senderEmailLabel" class="text-muted">&lt;{{ senderEmailLabel }}&gt;</span>
              </dd>
            </div>
            <div v-if="sentAtLabel" class="flex items-start gap-3">
              <dt class="w-16 shrink-0 text-xs font-semibold uppercase tracking-[0.06em] text-muted">
                Received
              </dt>
              <dd class="m-0 text-highlighted">{{ sentAtLabel }}</dd>
            </div>
          </dl>
        </header>

        <section aria-label="Email message">
          <h3 class="mb-2 text-xs font-semibold uppercase tracking-[0.06em] text-muted">Message</h3>
          <!-- Long bodies scroll internally instead of pushing the page (S4 spec). -->
          <div
            class="max-h-[min(30lh,50vh)] overflow-y-auto overscroll-contain break-words whitespace-pre-wrap rounded-xl border border-default bg-muted/40 p-4 text-sm leading-6 text-highlighted"
          >{{ displayBody }}</div>
        </section>
      </div>
    </template>

    <template #footer>
      <div class="flex items-center justify-between gap-3">
        <span class="text-xs text-muted">Read-only source email</span>
        <UButton color="neutral" variant="outline" @click="open = false">Close</UButton>
      </div>
    </template>
  </UModal>
</template>
