import { computed, onScopeDispose, shallowRef, watch, type Ref } from 'vue'
import { useToast } from '@nuxt/ui/composables/useToast'
import { ApiError } from '@/shared/http'
import { problemMessage, problemMessageText, type ProblemMessage } from '@/shared/problem-details'
import {
  getScreeningRules,
  previewScreeningRules,
  saveScreeningRules,
  type ScreeningRule,
  type ScreeningRuleSet,
  type ScreeningRulesPreview,
} from './api'
import { screeningRulesFormSchema } from './validation'

export type ScreeningRulesViewState = 'loading' | 'error' | 'empty' | 'ready'

const PREVIEW_DEBOUNCE_MS = 300

/**
 * The vacancy's Screening Rule set plus the editor lifecycle behind the
 * "Screening rules" dialog. Loading mirrors the form layout (a 404 is the
 * empty state, not a failure). The dialog owns its draft and announces edits;
 * this composable owns the live preview: latest-wins, debounced on edits,
 * fired immediately on open. The preview is strictly advisory — a failure
 * renders "Live count unavailable" and never blocks Save (the server owns
 * the invariants). Saving is modal-atomic: PUT, close, toast, then the one
 * refresh announcement reloads candidates and the vacancy header.
 */
export function useScreeningRules(
  vacancyId: Ref<string>,
  hasActiveRound: Ref<boolean>,
  onSaved: () => Promise<void>,
) {
  const toast = useToast()
  const ruleSet = shallowRef<ScreeningRuleSet | null>(null)
  const loadError = shallowRef<string | null>(null)
  // A 404 from the GET is the empty state (no rules yet), not a failure.
  const missing = shallowRef(false)
  let loadToken = 0

  const open = shallowRef(false)
  const saving = shallowRef(false)
  // Structured so the dialog can render it as an alert with its severity color.
  const saveError = shallowRef<ProblemMessage | null>(null)
  const preview = shallowRef<ScreeningRulesPreview | null>(null)
  const previewFailed = shallowRef(false)
  let previewToken = 0
  let previewTimer: ReturnType<typeof setTimeout> | null = null
  let lastPreviewKey: string | null = null

  const viewState = computed<ScreeningRulesViewState>(() => {
    if (loadError.value !== null) {
      return 'error'
    }
    if (missing.value) {
      return 'empty'
    }
    return ruleSet.value === null ? 'loading' : 'ready'
  })

  const ruleCount = computed(() => ruleSet.value?.rules.length ?? 0)

  async function load() {
    const token = ++loadToken
    loadError.value = null
    missing.value = false
    try {
      const loaded = await getScreeningRules(vacancyId.value)
      // Ignore stale responses when the route param changed meanwhile.
      if (token !== loadToken) {
        return
      }
      ruleSet.value = loaded
    } catch (error) {
      if (token !== loadToken) {
        return
      }
      if (error instanceof ApiError && error.status === 404) {
        missing.value = true
        ruleSet.value = null
        return
      }
      loadError.value = problemMessageText(
        problemMessage(error, "Couldn't load the screening rules"),
      )
    }
  }

  // A different vacancy starts with a clean slate.
  watch(
    vacancyId,
    () => {
      ruleSet.value = null
      loadError.value = null
      missing.value = false
      saveError.value = null
      open.value = false
      void load()
    },
    { immediate: true },
  )

  function cancelPreviewTimer() {
    if (previewTimer !== null) {
      clearTimeout(previewTimer)
      previewTimer = null
    }
  }

  function resetPreview() {
    cancelPreviewTimer()
    previewToken += 1
    lastPreviewKey = null
    preview.value = null
    previewFailed.value = false
  }

  /**
   * Previews the draft unless a guard says the count would mean nothing: no
   * active round (the footer shows its own note), an invalid draft (mid-edit
   * states like a blank value), or the same payload as the last request.
   */
  async function runPreview(token: number, rules: ScreeningRule[]) {
    if (!hasActiveRound.value || !screeningRulesFormSchema.safeParse({ rules }).success) {
      preview.value = null
      previewFailed.value = false
      return
    }
    try {
      const result = await previewScreeningRules(vacancyId.value, rules)
      if (token !== previewToken) {
        return
      }
      preview.value = result
      previewFailed.value = false
    } catch {
      if (token !== previewToken) {
        return
      }
      // Advisory only: the count region degrades, Save stays available.
      preview.value = null
      previewFailed.value = true
    }
  }

  function previewDraft(rules: ScreeningRule[], debounce: boolean) {
    const key = JSON.stringify(rules)
    if (key === lastPreviewKey) {
      return
    }
    lastPreviewKey = key
    cancelPreviewTimer()
    const token = ++previewToken
    if (!debounce) {
      void runPreview(token, rules)
      return
    }
    previewTimer = setTimeout(() => {
      previewTimer = null
      void runPreview(token, rules)
    }, PREVIEW_DEBOUNCE_MS)
  }

  /** Opens the editor with the saved rules as the opening draft. */
  function openEditor() {
    saveError.value = null
    open.value = true
    previewDraft((ruleSet.value?.rules ?? []).map((rule) => ({ ...rule })), false)
  }

  /** The dialog's latest draft; the preview follows after a quiet period. */
  function onDraftChange(rules: ScreeningRule[]) {
    previewDraft(rules, true)
  }

  // Closing the editor discards the draft and any in-flight preview.
  watch(open, (isOpen) => {
    if (!isOpen) {
      resetPreview()
    }
  })

  onScopeDispose(cancelPreviewTimer)

  async function save(rules: ScreeningRule[]): Promise<void> {
    if (saving.value) {
      return
    }
    saving.value = true
    saveError.value = null
    try {
      const saved = await saveScreeningRules(vacancyId.value, rules)
      ruleSet.value = saved
      missing.value = false
      open.value = false
      toast.add({ title: 'Screening rules saved', color: 'success' })
      // Modal-atomic cascade: reclassification is live on the read path, so
      // the candidate list and the vacancy header reload together.
      await onSaved()
    } catch (error) {
      // The dialog stays open with the failure rendered inline.
      saveError.value = problemMessage(error, "Couldn't save the screening rules")
    } finally {
      saving.value = false
    }
  }

  return {
    ruleSet,
    ruleCount,
    loadError,
    viewState,
    load,
    open,
    openEditor,
    onDraftChange,
    preview,
    previewFailed,
    saving,
    saveError,
    save,
  }
}
