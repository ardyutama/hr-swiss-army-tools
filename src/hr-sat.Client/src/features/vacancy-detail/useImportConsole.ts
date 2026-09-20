import { computed, shallowRef, type Ref } from 'vue'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import { useFormLayout } from '@/features/form-layout/useFormLayout'
import { useCandidateImport } from '@/features/candidates/useCandidateImport'
import {
  useFormResponseImport,
  type FormImportChange,
} from '@/features/import-form/useFormResponseImport'
import { useActionDialog } from '@/shared/useActionDialog'

export interface ImportConsoleInput {
  vacancyId: Ref<string>
  roundId: Ref<string>
  /**
   * The page's refresh announcements, invoked only from import outcomes and
   * layout saves — never during setup.
   */
  refreshVacancyAndCandidates: () => Promise<void>
  refreshVacancyCandidatesAndLayout: () => Promise<void>
}

/**
 * The vacancy-detail import console: composes the two Intake Source channels
 * (.eml and Form Response), the Form Layout they lean on, and the
 * import-request action dialog, and owns what crosses between them — the
 * request ∧ idle conjunction that shows the import dialog, the merged alert
 * and busy flags, the layout-edit request gated on idle, the layout-save
 * cascade, and the change dispatch to the page's refresh announcements.
 * Everything single-leaf (summary line, refusal state and intents) stays on
 * the leaves; the view binds it directly.
 */
export function useImportConsole(input: ImportConsoleInput) {
  const { vacancyId, roundId } = input

  const formLayout = useFormLayout(vacancyId)
  // The .eml adapter announces its own refresh; a landed import closes the dialog.
  const emlImport = useCandidateImport(vacancyId, roundId, input.refreshVacancyAndCandidates)
  // Layout-stamping outcomes (guided setup, drift confirm) move the layout too.
  const formImport = useFormResponseImport(
    vacancyId,
    roundId,
    formLayout.layout,
    async (changed: FormImportChange) => {
      if (changed === 'candidatesAndLayout') {
        await input.refreshVacancyCandidatesAndLayout()
      } else {
        await input.refreshVacancyAndCandidates()
      }
    },
  )

  const {
    open: importRequested,
    request: requestImport,
    confirm: confirmImport,
  } = useActionDialog({
    onReset: () => {
      emlImport.clearError()
      formImport.clearResult()
    },
  })

  // The layout edit request is console-owned UI state; it loses to any refusal step.
  const layoutEditRequested = shallowRef(false)
  const layoutEditing = computed({
    get: () => layoutEditRequested.value && formImport.idle.value,
    set: (open: boolean) => {
      layoutEditRequested.value = open
    },
  })

  // The import dialog shows only while requested and the import sits at idle —
  // a refusal closes it; terminal success returns to idle so it reappears with
  // the summary line while still requested.
  const importDialogOpen = computed({
    get: () => importRequested.value && formImport.idle.value,
    set: (open: boolean) => {
      importRequested.value = open
    },
  })

  // The dialog surfaces one alert at a time: the form flow's typed alert wins,
  // otherwise the .eml flow's message renders as a red inline alert.
  const importAlert = computed(() => {
    if (formImport.alert.value) {
      return formImport.alert.value
    }
    const emlError = emlImport.importError.value
    return emlError ? { color: 'error' as const, title: emlError } : null
  })

  const importBusy = computed(() => emlImport.importing.value || formImport.uploading.value)

  async function importEmlFiles(files: File[]) {
    formImport.clearResult()
    await confirmImport(() => emlImport.importFiles(files))
  }

  async function importCsvFile(file: File) {
    emlImport.clearError()
    // The dialog stays open so the summary line remains visible — a refusal
    // moves the step off idle, which swaps it for the guided panel or the
    // drift dialog via the derived bindings.
    await formImport.importFile(file)
  }

  function openLayoutEdit() {
    layoutEditRequested.value = true
  }

  function saveLayoutEdit(columns: FormLayoutColumn[]) {
    // A layout save re-projects over stored rows, so candidate details change too.
    void formLayout.save(columns).then(async (saved) => {
      if (saved) {
        await input.refreshVacancyAndCandidates()
        layoutEditRequested.value = false
      }
    })
  }

  return {
    // The composed leaves: the view binds their state and intents directly.
    formLayout,
    emlImport,
    formImport,
    requestImport,
    importDialogOpen,
    importAlert,
    importBusy,
    importEmlFiles,
    importCsvFile,
    layoutEditing,
    openLayoutEdit,
    saveLayoutEdit,
  }
}
