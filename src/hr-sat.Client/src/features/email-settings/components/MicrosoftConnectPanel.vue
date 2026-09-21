<script setup lang="ts">
import { computed, onScopeDispose, shallowRef } from 'vue'
import type { MicrosoftConnectState } from '../useMicrosoftConnect'
import { formatTimeHHmm } from '../format'

const props = defineProps<{
  /** The in-flight connect attempt's union (idle when no attempt was started). */
  connect: MicrosoftConnectState
  /** The saved connection's account email, when the row is Microsoft-signed-in. */
  connectedAs: string | null
  /** False disables the choice with an explanation (issue 03, decision 24). */
  available: boolean
}>()

const emit = defineEmits<{
  connect: []
}>()

const copied = shallowRef(false)
let copiedTimer: ReturnType<typeof setTimeout> | null = null

const challenge = computed(() =>
  props.connect.status === 'pending' ? props.connect.challenge : null,
)
const expiresAtText = computed(() =>
  challenge.value ? formatTimeHHmm(challenge.value.expiresAt) : '',
)
// The fresh success names its account while the settings reload lands; afterwards the
// saved row's account carries the line.
const connectedEmail = computed(() =>
  props.connect.status === 'succeeded' ? props.connect.accountEmail : props.connectedAs,
)

async function copyCode() {
  if (!challenge.value) {
    return
  }
  try {
    await navigator.clipboard.writeText(challenge.value.userCode)
  } catch {
    // Clipboard unavailable (non-secure context) — the code stays selectable.
    return
  }
  copied.value = true
  if (copiedTimer !== null) {
    clearTimeout(copiedTimer)
  }
  copiedTimer = setTimeout(() => {
    copied.value = false
  }, 1500)
}

onScopeDispose(() => {
  if (copiedTimer !== null) {
    clearTimeout(copiedTimer)
  }
})
</script>

<template>
  <div class="flex flex-col gap-3" data-testid="microsoft-connect-panel">
    <template v-if="connect.status === 'pending' && challenge">
      <p class="text-sm text-muted">
        Enter this code at
        <a
          :href="challenge.verificationUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="font-medium text-primary underline underline-offset-2"
        >microsoft.com/link</a>:
      </p>
      <div class="flex items-center gap-2">
        <code
          class="rounded-md border border-default bg-elevated px-3 py-1.5 font-mono text-base font-medium tracking-[0.2em]"
        >{{ challenge.userCode }}</code>
        <UButton
          color="neutral"
          variant="ghost"
          size="xs"
          :icon="copied ? 'i-lucide-check' : 'i-lucide-copy'"
          @click="copyCode"
        >
          {{ copied ? 'Copied' : 'Copy' }}
        </UButton>
      </div>
      <p class="flex items-center gap-2 text-sm text-muted" role="status">
        <span class="connect-pulse-dot" aria-hidden="true" />
        Waiting for sign-in — the code expires at {{ expiresAtText }}.
      </p>
    </template>

    <template v-else-if="connect.status === 'failed'">
      <UAlert
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        :title="connect.message"
        role="alert"
      />
      <div>
        <UButton color="neutral" variant="outline" @click="emit('connect')">
          Try again
        </UButton>
      </div>
    </template>

    <template v-else-if="connect.status === 'expired'">
      <p class="text-sm text-muted">The code expired before sign-in finished.</p>
      <div>
        <UButton color="neutral" variant="outline" @click="emit('connect')">
          Get a new code
        </UButton>
      </div>
    </template>

    <template v-else-if="connect.status === 'lost'">
      <p class="text-sm text-muted">
        The sign-in attempt was lost when the server restarted — start again.
      </p>
      <div>
        <UButton color="neutral" variant="outline" @click="emit('connect')">
          Connect Microsoft account
        </UButton>
      </div>
    </template>

    <template v-else-if="connect.status === 'succeeded' || connectedAs">
      <p class="flex items-center gap-2 text-sm" role="status">
        <UIcon name="i-lucide-circle-check" class="size-4 shrink-0 text-success" />
        Connected as {{ connectedEmail }}.
      </p>
    </template>

    <template v-else>
      <p class="text-sm text-muted">
        Sign in with the Microsoft account this app sends from.
      </p>
      <div>
        <UButton :disabled="!props.available" @click="emit('connect')">
          Connect Microsoft account
        </UButton>
      </div>
      <p v-if="!props.available" class="text-xs text-muted">
        Microsoft sign-in isn't set up on this installation — whoever installed the app
        can add the Microsoft client ID to the configuration file and restart the app.
      </p>
    </template>
  </div>
</template>

<style scoped>
/* Single pulse dot (issue 03, decision 33); ADR-0008 restrained motion — the
   animation only exists when the user allows motion. */
.connect-pulse-dot {
  width: 0.5rem;
  height: 0.5rem;
  flex-shrink: 0;
  border-radius: 9999px;
  background: var(--color-primary);
}

@media (prefers-reduced-motion: no-preference) {
  .connect-pulse-dot {
    animation: connect-pulse 1.6s ease-in-out infinite;
  }
}

@keyframes connect-pulse {
  0%,
  100% {
    opacity: 1;
  }

  50% {
    opacity: 0.25;
  }
}
</style>
