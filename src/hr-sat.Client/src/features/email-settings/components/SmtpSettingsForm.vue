<script setup lang="ts">
import { computed, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { FormSubmitEvent, RadioGroupItem } from '@nuxt/ui'
import { fieldErrorsOf, formErrorsOnly } from '@/shared/validation'
import type { SmtpSettings, SmtpSettingsWritePayload, SmtpTestPayload } from '../api'
import { smtpPresetForHost } from '../format'
import {
  FROM_NAME_MAX_LENGTH,
  smtpFormFieldKeys,
  smtpFormSchema,
  smtpPresets,
  smtpServerFieldNames,
  type SmtpFormOutput,
  type SmtpPresetId,
} from '../validation'

type TestState = 'idle' | 'passed' | 'failed'

const props = defineProps<{
  /** The effective settings the form prefills from; a new reference re-initializes the form. */
  settings: SmtpSettings
  saving: boolean
  testing: boolean
  removing: boolean
  /** Non-validation failure of the remove request, surfaced inside the confirm dialog. */
  removeError: string | null
}>()

const emit = defineEmits<{
  test: [payload: SmtpTestPayload]
  save: [payload: SmtpSettingsWritePayload]
  remove: []
}>()

const form = reactive({
  preset: 'gmail' as SmtpPresetId,
  host: '',
  port: '',
  username: '',
  password: '',
  fromAddress: '',
  fromName: '',
})
const formRef = useTemplateRef('formRef')
const testState = shallowRef<TestState>('idle')
const testMessage = shallowRef<string | null>(null)
const saveError = shallowRef<string | null>(null)
const removeOpen = shallowRef(false)

// Only a saved row's password carries over — a configuration file's password is never
// pre-filled and never assumed (decisions 6 and 9).
const hasStoredPassword = computed(
  () => props.settings.source === 'settings' && props.settings.hasPassword,
)

const schema = computed(() =>
  smtpFormSchema({ preset: form.preset, hasStoredPassword: hasStoredPassword.value }),
)

const passwordPlaceholder = computed(() =>
  hasStoredPassword.value ? 'Saved — type to replace' : 'Enter app password',
)

const presetItems = computed<RadioGroupItem[]>(() =>
  smtpPresets.map((preset) => ({
    value: preset.id,
    label: preset.label,
    description: preset.port === null ? 'Any SMTP host' : `${preset.host}:${preset.port}`,
  })),
)

const selectedPresetHelp = computed(
  () => smtpPresets.find((preset) => preset.id === form.preset)?.help ?? '',
)

// Prefill: saved values when they exist, otherwise the configuration file's values
// (decision 9); the password field always stays blank.
watch(
  () => props.settings,
  (settings) => {
    const preset = smtpPresetForHost(settings.host)
    form.preset = preset
    if (settings.host === null) {
      // Fresh setup: the selected preset fills host and port (decision 6:
      // preset-filled, editable).
      const defaults = smtpPresets.find((item) => item.id === preset)
      form.host = defaults?.host ?? ''
      form.port = defaults?.port?.toString() ?? ''
    } else {
      form.host = settings.host
      form.port = settings.port?.toString() ?? ''
    }
    form.username = settings.username ?? ''
    form.password = ''
    form.fromAddress = settings.fromAddress ?? ''
    form.fromName = settings.fromName ?? ''
    testState.value = 'idle'
    testMessage.value = null
    saveError.value = null
  },
  { immediate: true },
)

// Any edit after a passing test returns the form to must-test-again (decision 3).
watch(
  () => [form.host, form.port, form.username, form.password, form.fromAddress, form.fromName],
  () => {
    testState.value = 'idle'
    testMessage.value = null
    saveError.value = null
  },
)

function onPresetChange(value: unknown) {
  const preset = smtpPresets.find((item) => item.id === value)
  if (!preset) {
    return
  }
  form.preset = preset.id
  form.host = preset.host
  form.port = preset.port?.toString() ?? ''
}

function payloadsFrom(data: SmtpFormOutput): {
  test: SmtpTestPayload
  save: SmtpSettingsWritePayload
} {
  const password = data.password.trim().length > 0 ? data.password.trim() : null
  const fromName = data.fromName.trim().length > 0 ? data.fromName.trim() : null
  return {
    test: { host: data.host, port: data.port, username: data.username, password },
    save: {
      host: data.host,
      port: data.port,
      username: data.username,
      password,
      fromAddress: data.fromAddress,
      fromName,
    },
  }
}

function onTest() {
  testState.value = 'idle'
  testMessage.value = null
  const parsed = schema.value.safeParse(form)
  if (!parsed.success) {
    formRef.value?.setErrors(
      parsed.error.issues.map((issue) => ({
        name: String(issue.path[0]),
        message: issue.message,
      })),
    )
    return
  }
  emit('test', payloadsFrom(parsed.data).test)
}

function onSubmit(event: FormSubmitEvent<SmtpFormOutput>) {
  saveError.value = null
  emit('save', payloadsFrom(event.data).save)
}

defineExpose({
  /** The probe passed for the current values: Save unlocks until the next edit. */
  markTestPassed(message: string) {
    testState.value = 'passed'
    testMessage.value = message
  },
  /** The probe refused: the server's sanitized message shows inline, Save stays locked. */
  markTestFailed(message: string) {
    testState.value = 'failed'
    testMessage.value = message
  },
  /** Routes a failed save's ValidationProblem back onto the fields it names. */
  applyServerErrors(error: unknown) {
    const serverErrors = fieldErrorsOf(error)
    if (!serverErrors) {
      saveError.value = 'Could not save — try again.'
      return
    }
    const formErrors = formErrorsOnly(serverErrors, smtpFormFieldKeys)
    formRef.value?.setErrors(
      Object.entries(formErrors).flatMap(([key, messages]) =>
        messages.map((message) => ({
          name: smtpServerFieldNames[key] ?? key,
          message,
        })),
      ),
    )
  },
  closeRemoveDialog() {
    removeOpen.value = false
  },
})
</script>

<template>
  <section class="flex max-w-lg flex-col gap-5 rounded-xl border border-default bg-default p-5 shadow-sm">
    <p v-if="props.settings.source === 'configuration-file'" class="text-xs text-muted">
      Currently in effect from the configuration file — saving here replaces it.
    </p>

    <UForm
      ref="formRef"
      :schema="schema"
      :state="form"
      class="flex flex-col gap-5"
      @submit="onSubmit"
    >
      <UFormField label="Provider">
        <div class="flex flex-col gap-2">
          <URadioGroup
            :model-value="form.preset"
            :items="presetItems"
            variant="card"
            @update:model-value="onPresetChange"
          />
          <p v-if="selectedPresetHelp" class="text-xs text-muted">{{ selectedPresetHelp }}</p>
        </div>
      </UFormField>

      <div class="grid grid-cols-1 gap-5 sm:grid-cols-[1fr_8rem]">
        <UFormField label="Host" name="host" required>
          <UInput
            v-model="form.host"
            autocomplete="off"
            spellcheck="false"
            placeholder="smtp.gmail.com"
            class="w-full"
          />
        </UFormField>
        <UFormField label="Port" name="port" required>
          <UInput v-model="form.port" inputmode="numeric" autocomplete="off" class="w-full" />
        </UFormField>
      </div>

      <UFormField label="Username" name="username" required>
        <UInput v-model="form.username" autocomplete="off" class="w-full" />
      </UFormField>

      <UFormField
        label="Password"
        name="password"
        required
        help="App passwords are provider-specific."
      >
        <UInput
          v-model="form.password"
          type="password"
          autocomplete="new-password"
          :placeholder="passwordPlaceholder"
          class="w-full"
        />
      </UFormField>

      <UFormField label="From address" name="fromAddress" required>
        <UInput
          v-model="form.fromAddress"
          autocomplete="off"
          placeholder="hr@firma.example"
          class="w-full"
        />
      </UFormField>

      <UFormField label="From name" name="fromName">
        <UInput
          v-model="form.fromName"
          autocomplete="off"
          :maxlength="FROM_NAME_MAX_LENGTH"
          placeholder="HR Team"
          class="w-full"
        />
      </UFormField>

      <div class="flex items-center gap-2">
        <UButton
          color="neutral"
          variant="outline"
          :loading="props.testing"
          :disabled="props.saving"
          @click="onTest"
        >
          Test connection
        </UButton>
        <UButton
          :loading="props.saving"
          :disabled="testState !== 'passed' || props.testing"
          @click="formRef?.submit()"
        >
          Save
        </UButton>
      </div>
    </UForm>

    <UAlert
      v-if="props.testing"
      color="neutral"
      variant="subtle"
      icon="i-lucide-loader-circle"
      title="Testing…"
      role="status"
    />
    <UAlert
      v-else-if="testState === 'passed'"
      color="success"
      variant="subtle"
      icon="i-lucide-circle-check"
      :title="testMessage ?? 'Connected and authenticated.'"
      role="status"
    />
    <UAlert
      v-else-if="testState === 'failed'"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      :title="testMessage ?? 'Could not test the connection — try again.'"
      role="alert"
    />
    <UAlert
      v-if="saveError"
      color="error"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      :title="saveError"
      role="alert"
    />

    <div v-if="props.settings.source === 'settings'">
      <UButton
        color="error"
        variant="link"
        class="px-0"
        @click="removeOpen = true"
      >
        Remove saved settings
      </UButton>
    </div>

    <UModal
      v-model:open="removeOpen"
      title="Remove saved settings"
      :dismissible="!props.removing"
    >
      <template #body>
        <div class="flex flex-col gap-4">
          <p class="text-sm leading-relaxed text-muted">
            Remove the saved SMTP settings? The app will fall back to the configuration file
            if it exists.
          </p>
          <UAlert
            v-if="props.removeError"
            color="error"
            variant="subtle"
            icon="i-lucide-triangle-alert"
            :title="props.removeError"
            role="alert"
          />
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton
            color="neutral"
            variant="outline"
            :disabled="props.removing"
            @click="removeOpen = false"
          >
            Cancel
          </UButton>
          <UButton color="error" :loading="props.removing" @click="emit('remove')">
            Remove
          </UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>
