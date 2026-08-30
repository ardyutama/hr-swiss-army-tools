<script setup lang="ts">
import { computed, reactive, shallowRef, watch } from 'vue'
import { toast } from 'vue-sonner'
import AppButton from '@/shared/ui/AppButton.vue'
import AppDialog from '@/shared/ui/AppDialog.vue'
import AppField from '@/shared/ui/AppField.vue'
import AppIcon from '@/shared/ui/AppIcon.vue'
import {
  fieldErrorsOf,
  firstNonFieldError,
  formErrorsOnly,
  type FieldErrors,
} from '@/shared/validation'
import {
  createVacancy,
  getVacancy,
  updateVacancy,
  type VacancySummary,
  type VacancyWritePayload,
} from '../api'
import {
  vacancyFormFieldKeys,
  vacancyRequirementValues,
  type VacancyFormValues,
  validateVacancyForm,
} from '../validation'

const props = defineProps<{
  open: boolean
  vacancy: VacancySummary | null
}>()

const emit = defineEmits<{
  close: []
  saved: []
}>()

const isEdit = computed(() => props.vacancy !== null)
const dialogTitle = computed(() => (isEdit.value ? 'Edit vacancy' : 'Add vacancy'))

interface RequirementRow {
  id: number
  value: string
}

let nextRequirementId = 0

function requirementRow(value = ''): RequirementRow {
  return { id: nextRequirementId++, value }
}

const form = reactive({
  title: '',
  openedOn: '',
  requirements: [requirementRow()] as RequirementRow[],
})

const fieldErrors = shallowRef<FieldErrors>({})
const submitError = shallowRef<string | null>(null)
const submitting = shallowRef(false)
const initialFormPayload = shallowRef<VacancyWritePayload | null>(null)

function resetForm() {
  form.title = props.vacancy?.title ?? ''
  form.openedOn = props.vacancy?.openedOn ?? todayIso()
  form.requirements = [requirementRow()]
  initialFormPayload.value = null
  fieldErrors.value = {}
  submitError.value = null
}

function currentFormValues(): VacancyFormValues {
  return {
    title: form.title,
    openedOn: form.openedOn,
    requirements: form.requirements.map((row) => row.value),
  }
}

function payloadFromFormValues(values: VacancyFormValues): VacancyWritePayload {
  return {
    title: values.title.trim(),
    openedOn: values.openedOn,
    requirements: vacancyRequirementValues(values.requirements),
  }
}

function vacancyPayloadsEqual(
  left: VacancyWritePayload,
  right: VacancyWritePayload,
): boolean {
  return (
    left.title === right.title &&
    left.openedOn === right.openedOn &&
    left.requirements.length === right.requirements.length &&
    left.requirements.every((requirement, index) => requirement === right.requirements[index])
  )
}

watch(
  () => props.open,
  async (isOpen) => {
    if (!isOpen) {
      return
    }
    const editing = props.vacancy
    resetForm()
    if (!editing) {
      return
    }
    try {
      const details = await getVacancy(editing.id)
      // Ignore stale responses when the dialog was closed or retargeted meanwhile.
      if (!props.open || props.vacancy?.id !== editing.id) {
        return
      }
      const requirements = [...details.requirements].sort((a, b) => a.position - b.position)
      form.title = details.title
      form.openedOn = details.openedOn
      form.requirements =
        requirements.length > 0
          ? requirements.map((r) => requirementRow(r.phrase))
          : [requirementRow()]
      initialFormPayload.value = payloadFromFormValues({
        title: details.title,
        openedOn: details.openedOn,
        requirements: requirements.map((requirement) => requirement.phrase),
      })
    } catch {
      // Details are a best-effort prefill; the summary-backed form stays editable.
    }
  },
)

function todayIso(): string {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

function addRequirement() {
  form.requirements.push(requirementRow())
}

function removeRequirement(index: number) {
  form.requirements.splice(index, 1)
}

function firstError(key: string): string | undefined {
  return fieldErrors.value[key]?.[0]
}

async function submit() {
  submitError.value = null
  const values = currentFormValues()
  const clientErrors = validateVacancyForm(values)
  fieldErrors.value = clientErrors
  if (Object.keys(clientErrors).length > 0) {
    return
  }
  const payload = payloadFromFormValues(values)
  if (
    isEdit.value &&
    props.vacancy &&
    initialFormPayload.value &&
    vacancyPayloadsEqual(payload, initialFormPayload.value)
  ) {
    emit('close')
    return
  }
  submitting.value = true
  try {
    if (isEdit.value && props.vacancy) {
      await updateVacancy(props.vacancy.id, payload)
      toast.success(`Vacancy "${payload.title}" updated successfully`)
    } else {
      await createVacancy(payload)
      toast.success('Vacancy created successfully')
    }
    emit('saved')
    emit('close')
  } catch (error) {
    const serverErrors = fieldErrorsOf(error)
    if (serverErrors) {
      fieldErrors.value = formErrorsOnly(serverErrors, vacancyFormFieldKeys)
      const message =
        firstNonFieldError(serverErrors, vacancyFormFieldKeys) ??
        (isEdit.value ? 'Failed to update vacancy.' : 'Failed to create vacancy.')
      submitError.value = message
      toast.error(message)
    } else {
      const message = error instanceof Error ? error.message : 'Something went wrong.'
      submitError.value = message
      toast.error(message)
    }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <AppDialog :open="open" :title="dialogTitle" @close="emit('close')">
    <form class="vform" novalidate @submit.prevent="submit">
      <AppField label="Vacancy title" :error="firstError('title')">
        <template #default="{ id }">
          <input
            :id="id"
            v-model="form.title"
            type="text"
            placeholder="e.g. Senior Welder"
            maxlength="200"
            autocomplete="off"
          />
        </template>
      </AppField>

      <AppField label="Opening date" :error="firstError('openedon')">
        <template #default="{ id }">
          <input :id="id" v-model="form.openedOn" type="date" />
        </template>
      </AppField>

      <AppField
        label="Skill requirements"
        :error="firstError('requirements')"
        hint="CVs are sorted against these. Add at least one."
      >
        <template #default>
          <div class="vform__requirements">
            <div
              v-for="(row, index) in form.requirements"
              :key="row.id"
              class="vform__requirement-row"
            >
              <input
                v-model="row.value"
                type="text"
                :placeholder="`Requirement ${index + 1}`"
                maxlength="200"
                autocomplete="off"
                :aria-label="`Requirement ${index + 1}`"
              />
              <button
                type="button"
                class="vform__remove-req"
                :aria-label="`Remove requirement ${index + 1}`"
                :disabled="form.requirements.length === 1"
                @click="removeRequirement(index)"
              >
                <AppIcon name="close" :size="16" />
              </button>
            </div>
            <button type="button" class="vform__add-req" @click="addRequirement">
              <AppIcon name="plus" :size="16" />
              Add requirement
            </button>
          </div>
        </template>
      </AppField>
    </form>

    <template #footer>
      <AppButton variant="ghost" :disabled="submitting" @click="emit('close')">Cancel</AppButton>
      <AppButton :loading="submitting" @click="submit">
        {{ isEdit ? 'Save changes' : 'Create vacancy' }}
      </AppButton>
    </template>
  </AppDialog>
</template>

<style scoped>
.vform {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.vform__requirements {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.vform__requirement-row {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.vform__remove-req {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  flex-shrink: 0;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  transition: background 0.15s ease, color 0.15s ease;
}

.vform__remove-req:hover:not(:disabled) {
  background: var(--danger-soft);
  color: var(--danger);
}

.vform__remove-req:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.vform__add-req {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  align-self: flex-start;
  margin-top: var(--space-1);
  padding: 0.4rem 0.75rem;
  border: 1px dashed var(--border-strong);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--accent);
  font-size: var(--text-sm);
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s ease, border-color 0.15s ease;
}

.vform__add-req:hover {
  background: var(--accent-soft);
  border-color: var(--accent);
}
</style>
