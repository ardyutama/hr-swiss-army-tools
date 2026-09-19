import { computed, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { ApiError } from '@/shared/http'
import { problemMessage, problemMessageText, type ProblemMessage } from '@/shared/problem-details'
import { getFormLayout, saveFormLayout, type FormLayoutColumn, type FormLayoutDto } from './api'

export type FormLayoutViewState = 'loading' | 'error' | 'empty' | 'ready'

export function useFormLayout(vacancyId: Ref<string>) {
  const toast = useToast()
  const layout = shallowRef<FormLayoutDto | null>(null)
  const loadError = shallowRef<string | null>(null)
  // A 404 from the GET is the empty state (no layout yet), not a failure.
  const missing = shallowRef(false)
  const saving = shallowRef(false)
  // Structured so the panel can render it as an alert with its severity color.
  const saveError = shallowRef<ProblemMessage | null>(null)
  let requestToken = 0

  const viewState = computed<FormLayoutViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    if (missing.value) {
      return 'empty'
    }
    if (layout.value === null) {
      return 'loading'
    }
    return 'ready'
  })

  async function load() {
    const token = ++requestToken
    loadError.value = null
    missing.value = false
    try {
      const dto = await getFormLayout(vacancyId.value)
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== requestToken) {
        return
      }
      layout.value = dto
    } catch (error) {
      if (token !== requestToken) {
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        missing.value = true
        layout.value = null
        return
      }
      loadError.value = problemMessageText(problemMessage(error, "Couldn't load the form layout"))
    }
  }

  async function save(columns: FormLayoutColumn[]): Promise<boolean> {
    if (saving.value) {
      return false
    }
    saving.value = true
    saveError.value = null
    try {
      const saved = await saveFormLayout(vacancyId.value, columns)
      layout.value = saved
      missing.value = false
      announceSave(saved)
      return true
    } catch (error) {
      saveError.value = problemMessage(error, "Couldn't save the form layout")
      return false
    } finally {
      saving.value = false
    }
  }

  // A different vacancy starts with a clean slate.
  watch(
    vacancyId,
    () => {
      layout.value = null
      loadError.value = null
      missing.value = false
      saveError.value = null
      void load()
    },
    { immediate: true },
  )

  // Saving re-projects over stored rows; the toast states the back-fill so it
  // is not a silent surprise.
  function announceSave(saved: FormLayoutDto): void {
    toast.add({
      title: 'Form layout saved',
      description:
        saved.candidatesUpdated > 0
          ? backfillLine(saved.candidatesUpdated, saved.typedOverridesKept)
          : undefined,
      color: 'success',
    })
  }

  return { layout, loadError, viewState, saving, saveError, load, save }
}

function backfillLine(candidatesUpdated: number, typedOverridesKept: number): string {
  const updated =
    candidatesUpdated === 1
      ? 'Details re-filled for 1 candidate'
      : `Details re-filled for ${candidatesUpdated} candidates`
  const kept =
    typedOverridesKept === 0
      ? ''
      : typedOverridesKept === 1
        ? ' · 1 typed override kept'
        : ` · ${typedOverridesKept} typed overrides kept`
  return `${updated} from the form answers${kept}.`
}
