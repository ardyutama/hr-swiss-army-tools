<script setup lang="ts">
import { computed, reactive, shallowRef, watch } from 'vue'
import AppButton from '@/shared/ui/AppButton.vue'
import AppDialog from '@/shared/ui/AppDialog.vue'
import AppField from '@/shared/ui/AppField.vue'
import AppIcon from '@/shared/ui/AppIcon.vue'
import { fieldErrorsOf, type FieldErrors } from '@/shared/validation'
import {
  createVacancy,
  getVacancy,
  updateVacancy,
  type VacancySummary,
  type VacancyWritePayload,
} from '../api'

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

function resetForm() {
  form.title = props.vacancy?.title ?? ''
  form.openedOn = props.vacancy?.openedOn ?? todayIso()
  form.requirements = [requirementRow()]
  fieldErrors.value = {}
  submitError.value = null
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

function requirementValues(): string[] {
  return form.requirements.map((r) => r.value.trim()).filter((r) => r.length > 0)
}

function clientValidate(): boolean {
  const errors: FieldErrors = {}
  if (form.title.trim().length === 0) {
    errors.title = ['Title is required.']
  } else if (form.title.trim().length > 200) {
    errors.title = ['Title must be 200 characters or fewer.']
  }
  if (!form.openedOn) {
    errors.openedon = ['Opening date is required.']
  }
  if (requirementValues().length === 0) {
    errors.requirements = ['Add at least one requirement.']
  }
  fieldErrors.value = errors
  return Object.keys(errors).length === 0
}

function firstError(key: string): string | undefined {
  return fieldErrors.value[key]?.[0]
}

async function submit() {
  submitError.value = null
  if (!clientValidate()) {
    return
  }
  const payload: VacancyWritePayload = {
    title: form.title.trim(),
    openedOn: form.openedOn,
    requirements: requirementValues(),
  }
  submitting.value = true
  try {
    if (isEdit.value && props.vacancy) {
      await updateVacancy(props.vacancy.id, payload)
    } else {
      await createVacancy(payload)
    }
    emit('saved')
    emit('close')
  } catch (error) {
    const serverErrors = fieldErrorsOf(error)
    if (serverErrors) {
      fieldErrors.value = serverErrors
    } else {
      submitError.value = error instanceof Error ? error.message : 'Something went wrong.'
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

      <p v-if="submitError" class="vform__submit-error" role="alert">{{ submitError }}</p>
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

.vform__submit-error {
  margin: 0;
  padding: var(--space-3);
  border-radius: var(--radius-sm);
  background: var(--danger-soft);
  color: var(--danger);
  font-size: var(--text-sm);
  font-weight: 500;
}
</style>
