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
  () => [props.subject, props.sentAt],
  () => {
    open.value = false
  },
)

const senderLabel = computed(
  () => props.senderName ?? props.senderEmail ?? 'Unknown sender',
)

const meta = computed(() => {
  const parts = [senderLabel.value]
  if (props.sentAt) {
    parts.push(formatDate(props.sentAt))
  }
  return parts.join(' · ')
})
</script>

<template>
  <section
    class="rounded-xl border border-default bg-default shadow-sm"
    aria-label="Source email"
  >
    <div class="flex items-center gap-3 px-5 py-3.5">
      <UIcon name="i-lucide-mail-open" class="size-4 shrink-0 text-muted" aria-hidden="true" />
      <p class="m-0 min-w-0 flex-1 truncate text-sm text-muted">
        <span class="font-medium text-highlighted">{{ subject ?? '(no subject)' }}</span>
        — {{ meta }}
      </p>
      <UButton
        color="neutral"
        variant="ghost"
        size="sm"
        :icon="open ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
        :aria-expanded="open"
        @click="open = !open"
      >
        {{ open ? 'Close' : 'Open' }}
      </UButton>
    </div>

    <div v-if="open" class="border-t border-default px-5 py-4">
      <p class="m-0 text-sm font-semibold text-highlighted">{{ subject ?? '(no subject)' }}</p>
      <p class="m-0 mt-1 text-xs text-muted">
        From {{ senderLabel }}
        <template v-if="senderName && senderEmail"> &lt;{{ senderEmail }}&gt;</template>
        <template v-if="sentAt"> · {{ formatDate(sentAt) }}</template>
      </p>
      <!-- Long bodies scroll internally instead of pushing the page (S4 spec). -->
      <div
        class="mt-3 max-h-[30lh] overflow-y-auto whitespace-pre-wrap rounded-lg bg-muted/60 p-3 text-sm text-highlighted"
      >{{ body ?? 'This email has no body text.' }}</div>
    </div>
  </section>
</template>
