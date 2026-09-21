<script setup lang="ts">
import { computed, reactive, shallowRef, useTemplateRef, watch } from 'vue'
import type { FormSubmitEvent, RadioGroupItem } from '@nuxt/ui'
import { fieldErrorsOf, formErrorsOnly } from '@/shared/validation'
import type {
  SignInMethod,
  SmtpSettings,
  SmtpSettingsWritePayload,
  SmtpTestPayload,
} from '../api'
import { problemDetailText, smtpPresetForHost } from '../format'
import type { MicrosoftConnectState } from '../useMicrosoftConnect'
import MicrosoftConnectPanel from './MicrosoftConnectPanel.vue'
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
  /** The in-flight Microsoft connect attempt (idle when none was started). */
  connect: MicrosoftConnectState
}>()

const emit = defineEmits<{
  test: [payload: SmtpTestPayload]
  save: [payload: SmtpSettingsWritePayload]
  remove: []
  connect: []
}>()

const form = reactive({
  preset: 'gmail' as SmtpPresetId,
  method: 'app-password' as SignInMethod,
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
const switchAwayOpen = shallowRef(false)

// A switch-away captured while the confirm dialog is open; applied only on confirm
// (issue 03, decision 34 — nothing changes until then).
let pendingSwitchAway: (() => void) | null = null

// Only a saved row's password carries over — a configuration file's password is never
// pre-filled and never assumed (decisions 6 and 9).
const hasStoredPassword = computed(
  () =>
    props.settings.source === 'settings' &&
    props.settings.signInMethod === 'app-password' &&
    props.settings.hasPassword,
)

// The connected Microsoft Account: this session's fresh success first, then the saved
// row (issue 03, decisions 10, 35).
const isSavedMicrosoft = computed(
  () =>
    props.settings.source === 'settings' &&
    props.settings.signInMethod === 'microsoft-account' &&
    props.settings.username !== null,
)
const connectedEmail = computed(() =>
  props.connect.status === 'succeeded' && props.connect.accountEmail !== ''
    ? props.connect.accountEmail
    : isSavedMicrosoft.value
      ? props.settings.username
      : null,
)
const isConnected = computed(
  () => connectedEmail.value !== null && connectedEmail.value !== '',
)

const schema = computed(() =>
  smtpFormSchema({
    preset: form.preset,
    method: form.method,
    hasStoredPassword: hasStoredPassword.value,
  }),
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

const methodItems: RadioGroupItem[] = [
  { value: 'app-password', label: 'App password', description: 'Type an app password from the account.' },
  { value: 'microsoft-account', label: 'Microsoft account', description: 'Sign in through Microsoft.' },
]

// Save gating per method (issue 03, decision 35): App Password keeps the
// test-before-save gate; Microsoft Account hides the test and gates on a live
// connection plus valid From fields — editing only the From name under an existing
// connection keeps Save enabled.
const saveEnabled = computed(() => {
  if (props.saving || props.testing) {
    return false
  }
  if (form.method === 'microsoft-account') {
    return isConnected.value && schema.value.safeParse(form).success
  }
  return testState.value === 'passed'
})

// The switch-away confirm names the account it would discard (decision 34).
const switchAwayCopy = computed(() =>
  connectedEmail.value
    ? `Saving after switching disconnects ${connectedEmail.value} — the Microsoft sign-in is discarded when you save. Nothing changes until then.`
    : 'Saving after switching discards the Microsoft sign-in choice. Nothing changes until you save.',
)

// Prefill: saved values when they exist, otherwise the configuration file's values
// (decision 9); the password field always stays blank. The Sign-in Method rides along
// (decision 11) — a saved Microsoft account resolves to the Outlook preset, so the
// method row shows and the connected state renders (decision 25).
watch(
  () => props.settings,
  (settings) => {
    const preset = smtpPresetForHost(settings.host)
    form.preset = preset
    form.method = settings.signInMethod ?? 'app-password'
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

function applyPreset(presetId: SmtpPresetId) {
  const preset = smtpPresets.find((item) => item.id === presetId)
  if (!preset) {
    return
  }
  form.preset = preset.id
  form.host = preset.host
  form.port = preset.port?.toString() ?? ''
}

function onPresetChange(value: unknown) {
  const preset = smtpPresets.find((item) => item.id === value)
  if (!preset || preset.id === form.preset) {
    return
  }
  // Leaving Outlook retires the Sign-in Method row with it (decision 25); while
  // Microsoft is selected the switch confirms first (decision 34).
  if (preset.id !== 'outlook' && form.method === 'microsoft-account') {
    pendingSwitchAway = () => {
      applyPreset(preset.id)
      form.method = 'app-password'
    }
    switchAwayOpen.value = true
    return
  }
  applyPreset(preset.id)
}

function onMethodChange(value: unknown) {
  if (value !== 'app-password' && value !== 'microsoft-account') {
    return
  }
  if (value === form.method) {
    return
  }
  // Microsoft → App Password while connected confirms first (decision 34); the
  // reverse direction needs no confirm.
  if (value === 'app-password' && form.method === 'microsoft-account' && isConnected.value) {
    pendingSwitchAway = () => {
      form.method = 'app-password'
    }
    switchAwayOpen.value = true
    return
  }
  form.method = value
}

function confirmSwitchAway() {
  const apply = pendingSwitchAway
  pendingSwitchAway = null
  switchAwayOpen.value = false
  apply?.()
}

function cancelSwitchAway() {
  pendingSwitchAway = null
  switchAwayOpen.value = false
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
  const data = parsed.data
  if (data.method !== 'app-password') {
    return
  }
  emit('test', {
    host: data.host,
    port: data.port,
    username: data.username,
    password: data.password.trim().length > 0 ? data.password.trim() : null,
  })
}

function onSubmit(event: FormSubmitEvent<SmtpFormOutput>) {
  saveError.value = null
  const data = event.data
  if (data.method === 'microsoft-account') {
    // Only the From fields cross the wire under Microsoft sign-in (decision 22).
    emit('save', {
      signInMethod: 'microsoft-account',
      fromAddress: data.fromAddress,
      fromName: data.fromName.length > 0 ? data.fromName : null,
    })
    return
  }
  emit('save', {
    signInMethod: 'app-password',
    host: data.host,
    port: data.port,
    username: data.username,
    password: data.password.trim().length > 0 ? data.password.trim() : null,
    fromAddress: data.fromAddress,
    fromName: data.fromName.length > 0 ? data.fromName : null,
  })
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
      // A non-validation refusal (e.g. no connected grant) shows the server's copy.
      saveError.value = problemDetailText(error) ?? 'Could not save — try again.'
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

      <!-- Progressive disclosure (issue 03, decision 25): the Sign-in Method row
           exists only for the Outlook preset. -->
      <UFormField v-if="form.preset === 'outlook'" label="Sign-in method">
        <URadioGroup
          :model-value="form.method"
          :items="methodItems"
          variant="card"
          @update:model-value="onMethodChange"
        />
      </UFormField>

      <template v-if="form.method === 'app-password'">
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
      </template>

      <MicrosoftConnectPanel
        v-else
        :connect="props.connect"
        :connected-as="isSavedMicrosoft ? props.settings.username : null"
        :available="props.settings.microsoftSignInAvailable"
        @connect="emit('connect')"
      />

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
        <!-- The test is an App-Password-method concept; connect is the test under
             Microsoft sign-in (issue 03, decision 6). -->
        <UButton
          v-if="form.method === 'app-password'"
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
          :disabled="!saveEnabled"
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
      v-else-if="form.method === 'app-password' && testState === 'passed'"
      color="success"
      variant="subtle"
      icon="i-lucide-circle-check"
      :title="testMessage ?? 'Connected and authenticated.'"
      role="status"
    />
    <UAlert
      v-else-if="form.method === 'app-password' && testState === 'failed'"
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
        {{ isSavedMicrosoft ? 'Disconnect…' : 'Remove saved settings' }}
      </UButton>
    </div>

    <UModal
      v-model:open="removeOpen"
      :title="isSavedMicrosoft ? 'Disconnect Microsoft account' : 'Remove saved settings'"
      :dismissible="!props.removing"
    >
      <template #body>
        <div class="flex flex-col gap-4">
          <p class="text-sm leading-relaxed text-muted">
            {{ isSavedMicrosoft
              ? `Disconnect ${props.settings.username}? The Microsoft sign-in is discarded — the app will fall back to the configuration file if it exists. To stop this app's access entirely, revoke it in your Microsoft account settings.`
              : 'Remove the saved SMTP settings? The app will fall back to the configuration file if it exists.' }}
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
            {{ isSavedMicrosoft ? 'Disconnect' : 'Remove' }}
          </UButton>
        </div>
      </template>
    </UModal>

    <!-- The switch-away confirm (issue 03, decision 34): no server call until Save. -->
    <UModal v-model:open="switchAwayOpen" title="Switch to App Password?">
      <template #body>
        <p class="text-sm leading-relaxed text-muted">{{ switchAwayCopy }}</p>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="outline" @click="cancelSwitchAway">
            Keep Microsoft account
          </UButton>
          <UButton @click="confirmSwitchAway">
            Switch to App Password
          </UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>
