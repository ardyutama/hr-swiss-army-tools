<script setup lang="ts">
import { onMounted, shallowRef, useTemplateRef, watch } from 'vue'
import type {
  SmtpSettingsWritePayload,
  SmtpTestPayload,
} from '@/features/email-settings/api'
import SmtpSettingsForm from '@/features/email-settings/components/SmtpSettingsForm.vue'
import SmtpStatusLine from '@/features/email-settings/components/SmtpStatusLine.vue'
import { problemDetailText } from '@/features/email-settings/format'
import { useEmailSettings } from '@/features/email-settings/useEmailSettings'
import { useMicrosoftConnect } from '@/features/email-settings/useMicrosoftConnect'
import { problemMessage, problemMessageText } from '@/shared/problem-details'

const {
  settings,
  loadError,
  viewState,
  saving,
  testing,
  removing,
  load,
  save,
  testConnection,
  removeSettings,
} = useEmailSettings()

// One composable per async lifecycle (issue 03, decision 26): the connect attempt is
// owned here; the form renders it. A succeeded attempt means the server already wrote
// the row (connect is the test, decision 6) — reload so the form and the status line
// show the derived account.
const microsoftConnect = useMicrosoftConnect()

watch(
  () => microsoftConnect.state.value.status,
  async (status) => {
    if (status === 'succeeded') {
      await load()
    }
  },
)

const form = useTemplateRef<InstanceType<typeof SmtpSettingsForm>>('form')
const removeError = shallowRef<string | null>(null)

onMounted(load)

async function onTest(payload: SmtpTestPayload) {
  try {
    const result = await testConnection(payload)
    form.value?.markTestPassed(result.message)
  } catch (error) {
    // The test endpoint's 400 carries the stage-tagged, sanitized message verbatim;
    // anything else gets the generic failure copy (layout doc section E).
    form.value?.markTestFailed(
      problemDetailText(error) ?? 'Could not test the connection — try again.',
    )
  }
}

async function onSave(payload: SmtpSettingsWritePayload) {
  try {
    await save(payload)
  } catch (error) {
    form.value?.applyServerErrors(error)
  }
}

async function onRemove() {
  removeError.value = null
  try {
    await removeSettings()
    // Disconnecting ends any connect session the panel could still trade on.
    microsoftConnect.reset()
    form.value?.closeRemoveDialog()
  } catch (error) {
    removeError.value = problemMessageText(
      problemMessage(error, 'Could not remove the saved settings'),
    )
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <header class="flex flex-col gap-1">
      <h1 class="text-2xl font-bold tracking-tight">Email sending</h1>
      <p class="max-w-[65ch] text-sm text-ink/60">
        Set the account this installation uses to send email.
      </p>
      <SmtpStatusLine v-if="settings" :settings="settings" class="mt-2" />
    </header>

    <section
      v-if="viewState === 'loading'"
      class="flex max-w-lg flex-col gap-5 rounded-xl border border-default bg-default p-5 shadow-sm"
      aria-busy="true"
      aria-label="Loading email settings"
    >
      <USkeleton class="h-3 w-20" />
      <div class="grid grid-cols-3 gap-2">
        <USkeleton class="h-16" />
        <USkeleton class="h-16" />
        <USkeleton class="h-16" />
      </div>
      <USkeleton class="h-9 w-full" />
      <USkeleton class="h-9 w-full" />
      <USkeleton class="h-9 w-full" />
    </section>

    <UAlert
      v-else-if="viewState === 'error'"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      :title="loadError ?? 'Couldn\'t load the email settings'"
      role="alert"
    />

    <SmtpSettingsForm
      v-else-if="settings"
      ref="form"
      :settings="settings"
      :saving="saving"
      :testing="testing"
      :removing="removing"
      :remove-error="removeError"
      :connect="microsoftConnect.state.value"
      @test="onTest"
      @save="onSave"
      @remove="onRemove"
      @connect="microsoftConnect.begin"
    />
  </div>
</template>
