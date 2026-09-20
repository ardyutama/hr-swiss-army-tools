import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, shallowRef, toRef } from 'vue'

import ImportDialog from '@/features/import/components/ImportDialog.vue'
import GuidedSetupDialog from '@/features/form-layout/components/GuidedSetupDialog.vue'
import FormLayoutEditDialog from '@/features/form-layout/components/FormLayoutEditDialog.vue'
import DriftDialog from '@/features/import-form/components/DriftDialog.vue'
import FormLayoutSummary from '@/features/form-layout/components/FormLayoutSummary.vue'
import ImportResultList from '@/features/candidates/components/ImportResultList.vue'
import type { FormLayoutColumn } from '@/features/form-layout/api'
import { useImportConsole } from './useImportConsole'

// The import console has no production wrapper component: the page binds it.
// This spec-local harness (testing.md: spec-local harness admission) wires the
// composer to the flow's top dialogs and entry chrome so the console's
// scenarios run at the seam. It is scaffolding local to this spec — never
// production code.
const ImportConsoleHarness = defineComponent({
  props: { id: { type: String, required: true } },
  setup(props) {
    const vacancyId = toRef(props, 'id')
    const roundId = shallowRef('1')
    const importConsole = useImportConsole({
      vacancyId,
      roundId,
      refreshVacancyAndCandidates,
      // The layout leg of the announcement reloads for real so the summary
      // card reflects saves; vacancy/candidates have no harness surface.
      refreshVacancyCandidatesAndLayout: async () => {
        await refreshVacancyCandidatesAndLayout()
        await importConsole.formLayout.load()
      },
    })
    const { formImport } = importConsole
    const { layout, loadError, viewState, saving, saveError } = importConsole.formLayout
    const { results } = importConsole.emlImport

    return () =>
      h('div', [
        h(
          'button',
          { type: 'button', onClick: () => importConsole.requestImport() },
          'Import candidates',
        ),
        h(FormLayoutSummary, {
          state: viewState.value,
          layout: layout.value,
          loadError: loadError.value,
          canEdit: true,
          onEdit: () => importConsole.openLayoutEdit(),
          onRetry: () => importConsole.formLayout.load(),
        }),
        results.value && results.value.length > 0
          ? h('section', { 'aria-label': 'Last import results' }, [
              h(ImportResultList, { results: results.value }),
            ])
          : null,
        h(ImportDialog, {
          open: importConsole.importDialogOpen.value,
          'onUpdate:open': (open: boolean) => {
            importConsole.importDialogOpen.value = open
          },
          busy: importConsole.importBusy.value,
          alert: importConsole.importAlert.value,
          summary: formImport.summaryLine.value,
          onEmlFiles: (files: File[]) => importConsole.importEmlFiles(files),
          onCsvFile: (file: File) => importConsole.importCsvFile(file),
        }),
        h(GuidedSetupDialog, {
          open: formImport.guidedSetupOpen.value,
          'onUpdate:open': (open: boolean) => {
            formImport.guidedSetupOpen.value = open
          },
          headers: formImport.guidedSetup.value?.headers ?? [],
          initialColumns: layout.value?.columns ?? null,
          initialHeaders: layout.value?.headerSnapshot ?? null,
          fileName: formImport.guidedSetup.value?.file.name ?? null,
          busy: formImport.guidedSetup.value?.submitting ?? false,
          alert: formImport.alert.value,
          onSave: (columns: FormLayoutColumn[]) => formImport.submitLayout(columns),
          onCancel: () => formImport.cancel(),
        }),
        h(FormLayoutEditDialog, {
          open: importConsole.layoutEditing.value,
          'onUpdate:open': (open: boolean) => {
            importConsole.layoutEditing.value = open
          },
          headers: layout.value?.headerSnapshot ?? [],
          initialColumns: layout.value?.columns ?? null,
          busy: saving.value,
          alert: saveError.value,
          onSave: (columns: FormLayoutColumn[]) => importConsole.saveLayoutEdit(columns),
        }),
        h(DriftDialog, {
          open: formImport.headerDriftOpen.value,
          'onUpdate:open': (open: boolean) => {
            formImport.headerDriftOpen.value = open
          },
          changes: formImport.headerDrift.value?.changes ?? [],
          busy: formImport.headerDrift.value?.submitting ?? false,
          alert: formImport.alert.value,
          onConfirm: () => formImport.confirmDrift(),
          onRemap: () => formImport.remap(),
          onCancel: () => formImport.cancel(),
        }),
      ])
  },
})

const toastAdd = vi.hoisted(() => vi.fn())

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

// The page's refresh announcements, recorded: the console's contract with the
// page is the announcement itself.
const refreshVacancyAndCandidates = vi.fn(async () => {})
const refreshVacancyCandidatesAndLayout = vi.fn(async () => {})

function mountConsole() {
  return mount(ImportConsoleHarness, { props: { id: '1' } })
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

type FetchHandler = (url: string, init?: RequestInit) => Promise<Response> | undefined

// The console touches four endpoints: the Form Layout GET/PUT and the two
// import POSTs. Anything else is unstubbed and fails the test.
function stubFetch(layout: () => unknown, handler: FetchHandler): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const matched = handler(url, init)
      if (matched !== undefined) {
        return matched
      }
      if (url.endsWith('/form-layout') && (init?.method ?? 'GET') === 'GET') {
        return Promise.resolve(layout() as Response)
      }
      throw new Error(`Unstubbed fetch: ${init?.method ?? 'GET'} ${url}`)
    }),
  )
}

function noLayout() {
  return jsonResponse({ title: 'FormLayouts.NotFound', detail: 'No form layout.' }, 404)
}

// The Form Layout GET wire shape spells roles lowercased; writes take camelCase.
function formLayoutDto(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id: 5,
    vacancyId: 1,
    headerSnapshot: ['Timestamp', 'Nama Lengkap', 'Email aktif', 'Pengalaman kerja'],
    columns: [
      { ordinal: 1, role: 'name', label: 'Candidate name' },
      { ordinal: 2, role: 'contactemail', label: null },
      { ordinal: 3, role: null, label: null },
    ],
    isValid: true,
    candidatesUpdated: 0,
    typedOverridesKept: 0,
    ...overrides,
  }
}

function bodyElement(selector: string): Element {
  const element = document.body.querySelector(selector)
  if (!element) {
    throw new Error(`Expected "${selector}" to exist in document.body`)
  }
  return element
}

function dialogButton(text: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((button) =>
    button.textContent?.includes(text),
  )
}

async function openImportDialog(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes('Import candidates'))
  expect(button, 'an Import candidates button').toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

function dropFiles(files: File[]) {
  return new DOMWrapper(bodyElement('.dropzone')).trigger('drop', { dataTransfer: { files } })
}

// Picks a column in one of the Form Layout panel's role selects (teleported).
async function pickRoleColumn(roleLabel: string, optionText: string) {
  const trigger = Array.from(document.body.querySelectorAll('button')).find(
    (candidate) => candidate.getAttribute('aria-label') === roleLabel,
  )
  expect(trigger, `a "${roleLabel}" role select`).toBeDefined()
  await new DOMWrapper(trigger as HTMLElement).trigger('keydown', { key: 'ArrowDown' })
  await flushPromises()
  const option = Array.from(document.body.querySelectorAll<HTMLElement>('[role="option"]')).find(
    (candidate) => candidate.textContent?.includes(optionText),
  )
  expect(option, `an option containing "${optionText}"`).toBeDefined()
  await new DOMWrapper(option as HTMLElement).trigger('keydown', { key: 'Enter' })
  await flushPromises()
}

// Clicks the summary card's Edit layout action (the edit flow's entry point).
async function openLayoutEdit(wrapper: VueWrapper) {
  const button = wrapper
    .findAll('button')
    .find((candidate) => candidate.text().includes('Edit layout'))
  expect(button, 'the summary card’s Edit layout button').toBeDefined()
  await button!.trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('useImportConsole · candidate import and Form Layout', () => {
  it('US-12: HR drops .eml files into a vacancy and sees each file’s outcome', async () => {
    let sentFileNames: string[] | undefined
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        const form = init.body as FormData
        sentFileNames = form.getAll('files').map((entry) => (entry as File).name)
        return Promise.resolve(
          jsonResponse({
            results: [
              {
                fileName: 'alice.eml',
                status: 'imported',
                error: null,
                candidate: null,
              },
              {
                fileName: 'broken.eml',
                status: 'failed',
                error: 'The .eml file must contain at least one valid PDF attachment.',
                candidate: null,
              },
            ],
          }),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
      new File(['broken source'], 'broken.eml', { type: 'message/rfc822' }),
    ])
    await flushPromises()

    // Every dropped file is submitted as a repeated multipart `files` entry.
    expect(sentFileNames).toEqual(['alice.eml', 'broken.eml'])

    // Each file's outcome is visible to HR…
    expect(wrapper.text()).toContain('alice.eml')
    expect(wrapper.text()).toContain('Imported')
    expect(wrapper.text()).toContain('broken.eml')
    expect(wrapper.text()).toContain('Failed')
    expect(wrapper.text()).toContain('at least one valid PDF attachment')
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Import complete: 1 imported, 1 failed',
        color: 'success',
      }),
    )
    // …and the console announces the change so the page can refresh.
    expect(refreshVacancyAndCandidates).toHaveBeenCalled()
    wrapper.unmount()
  })

  it('US-12: HR is told when the import request fails', async () => {
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
        return Promise.resolve(
          jsonResponse({ errors: { files: ['At least one .eml file is required.'] } }, 400),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    // The dialog stays open with the failure visible next to the drop zone.
    expect(document.body.textContent).toContain('At least one .eml file is required.')
    expect(document.body.querySelector('.dropzone')).not.toBeNull()
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: HR drops a Google Forms .csv export and sees the Form Response import summary', async () => {
    let sentFileName: string | undefined
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
        const form = init.body as FormData
        sentFileName = (form.get('file') as File).name
        return Promise.resolve(
          jsonResponse({
            rowsRead: 214,
            created: 198,
            updated: 12,
            skippedOutdated: 4,
            priorApplications: 9,
          }),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    // The single .csv is dispatched to the form import endpoint as one multipart file.
    expect(sentFileName).toBe('responses.csv')

    // The dialog stays open with the summary line visible…
    expect(document.body.querySelector('.dropzone')).not.toBeNull()
    expect(document.body.querySelector('[role="status"]')?.textContent).toContain(
      '214 rows read · 198 new · 12 updated (resubmitted) · 4 skipped (outdated) · 9 prior applications noticed',
    )
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title:
          'Form import complete: 214 rows read · 198 new · 12 updated (resubmitted) · 4 skipped (outdated) · 9 prior applications noticed',
        color: 'success',
      }),
    )
    // …and the console announces the change so the page can refresh behind it.
    expect(refreshVacancyAndCandidates).toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: a mixed drop of .eml and .csv files is rejected inline and nothing uploads', async () => {
    const posted: string[] = []
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST') {
        posted.push(url)
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
      new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' }),
    ])
    await flushPromises()

    expect(posted).toEqual([])
    expect(document.body.textContent).toContain('Nothing was uploaded')
    expect(document.body.textContent).toContain('alice.eml')
    expect(document.body.textContent).toContain('drop .eml files on their own')
    expect(document.body.textContent).toContain('responses.csv')
    expect(document.body.textContent).toContain('drop one .csv export on its own')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: a wrong-type drop is rejected inline and nothing uploads', async () => {
    const posted: string[] = []
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST') {
        posted.push(url)
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['notes'], 'notes.txt', { type: 'text/plain' })])
    await flushPromises()

    expect(posted).toEqual([])
    expect(document.body.textContent).toContain('Nothing was uploaded')
    expect(document.body.textContent).toContain('notes.txt')
    expect(document.body.textContent).toContain("isn't an .eml or a .csv export")
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: a malformed CSV shows a red alert naming the row position', async () => {
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
        return Promise.resolve(
          jsonResponse({ errors: { file: ['Row 12 has 7 cells but the header has 9.'] } }, 400),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    // The dialog stays open with the malformed-CSV failure visible next to the drop zone.
    const alert = bodyElement('[role="alert"]')
    expect(alert.textContent).toContain("Couldn't read that CSV")
    expect(alert.textContent).toContain('Row 12 has 7 cells but the header has 9.')
    expect(alert.className).toContain('error')
    expect(document.body.querySelector('.dropzone')).not.toBeNull()
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: importing into a closed round shows the amber lifecycle re-expression', async () => {
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
        return Promise.resolve(jsonResponse({ title: 'IntakeRounds.Closed' }, 409))
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    const alert = bodyElement('[role="alert"]')
    expect(alert.textContent).toContain('This round is closed')
    expect(alert.textContent).toContain('Closed rounds are read-only')
    expect(alert.className).toContain('warning')
    expect(document.body.querySelector('.dropzone')).not.toBeNull()
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: importing a form export into a closed vacancy shows the amber lifecycle re-expression', async () => {
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
        return Promise.resolve(
          jsonResponse(
            { title: 'Candidates.FormImportLifecycleConflict', detail: 'The vacancy is invalid.' },
            409,
          ),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    const alert = bodyElement('[role="alert"]')
    expect(alert.textContent).toContain('This vacancy is closed')
    expect(alert.textContent).toContain('Reopen it to keep importing candidates.')
    expect(alert.className).toContain('warning')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: the first .csv upload routes into the guided Form Layout setup and completes the import', async () => {
    let imported = false
    let layoutPayload: string | undefined
    let sentFileName: string | undefined
    stubFetch(
      () => (imported ? jsonResponse(formLayoutDto()) : noLayout()),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          const form = init.body as FormData
          if (form.get('layout') === null) {
            // No valid layout yet: the import is refused and carries the file's headers.
            return Promise.resolve(
              jsonResponse(
                {
                  title: 'Candidates.FormLayoutRequired',
                  detail: 'The vacancy needs a valid Form Layout first.',
                  headers: ['Timestamp', 'Nama Lengkap', 'Email aktif', 'Pengalaman kerja'],
                },
                409,
              ),
            )
          }
          imported = true
          layoutPayload = form.get('layout') as string
          sentFileName = (form.get('file') as File).name
          return Promise.resolve(
            jsonResponse({
              rowsRead: 2,
              created: 2,
              updated: 0,
              skippedOutdated: 0,
              priorApplications: 0,
            }),
          )
        }
        return undefined
      },
    )

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Nama'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    // The import dialog is swapped for the blocking guided panel naming the held file…
    expect(document.body.querySelector('.dropzone')).toBeNull()
    expect(document.body.textContent).toContain('Set up the form layout to finish importing')
    expect(document.body.textContent).toContain('responses.csv')
    expect(document.body.textContent).toContain('Bind Name and Contact email')
    // …with no dismiss control — the only exits are Cancel import and Save.
    expect(document.body.querySelector('button[aria-label="Close"]')).toBeNull()

    // HR binds the required roles by hand — the panel never pre-fills them.
    await pickRoleColumn('Name', '1 · Nama Lengkap')
    await pickRoleColumn('Contact email', '2 · Email aktif')

    dialogButton('Save layout & import')?.click()
    await flushPromises()

    // The held file is re-sent with the mapping as the layout payload (camelCase roles).
    expect(sentFileName).toBe('responses.csv')
    expect(JSON.parse(layoutPayload!)).toEqual({
      columns: [
        { ordinal: 1, role: 'name', label: null },
        { ordinal: 2, role: 'contactEmail', label: null },
      ],
    })

    // Success lands on the normal import result: toast plus the summary line…
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Form import complete: 2 rows read · 2 new',
        color: 'success',
      }),
    )
    expect(document.body.querySelector('[role="status"]')?.textContent).toContain(
      '2 rows read · 2 new',
    )
    // …the layout-stamping change is announced so the page reloads the layout…
    expect(refreshVacancyCandidatesAndLayout).toHaveBeenCalled()
    // …and the stamped layout shows in the summary card (the harness runs that
    // leg of the announcement for real).
    expect(wrapper.text()).toContain('1 · Nama Lengkap')
    wrapper.unmount()
  })

  it('domain: Header Drift pauses a re-upload behind the drift dialog; Confirm imports with confirmDrift', async () => {
    const confirmDriftValues: (FormDataEntryValue | null)[] = []
    stubFetch(
      () => jsonResponse(formLayoutDto()),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          const form = init.body as FormData
          confirmDriftValues.push(form.get('confirmDrift'))
          if (form.get('confirmDrift') !== 'true') {
            return Promise.resolve(
              jsonResponse(
                {
                  title: 'Candidates.FormHeaderDrift',
                  detail: 'The uploaded form headers differ from the saved Form Layout.',
                  headers: ['Timestamp', 'Nama lengkap sesuai KTP', 'Email aktif', 'Pengalaman kerja'],
                  changes: [
                    { ordinal: 1, was: 'Nama Lengkap', now: 'Nama lengkap sesuai KTP' },
                  ],
                },
                409,
              ),
            )
          }
          return Promise.resolve(
            jsonResponse({
              rowsRead: 3,
              created: 1,
              updated: 2,
              skippedOutdated: 0,
              priorApplications: 0,
            }),
          )
        }
        return undefined
      },
    )

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Nama'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    // The drift dialog lists the changed ordinal with its role, label, and both headers.
    expect(document.body.textContent).toContain('Form headers changed')
    expect(document.body.textContent).toContain('bound to Name')
    expect(document.body.textContent).toContain('Candidate name')
    expect(document.body.textContent).toContain('Nama Lengkap')
    expect(document.body.textContent).toContain('Nama lengkap sesuai KTP')

    dialogButton('Confirm mapping & import')?.click()
    await flushPromises()

    // The held file is re-sent with the confirmDrift flag, and the import lands.
    expect(confirmDriftValues).toEqual([null, 'true'])
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Form import complete: 3 rows read · 1 new · 2 updated (resubmitted)',
        color: 'success',
      }),
    )
    expect(document.body.querySelector('[role="status"]')?.textContent).toContain('3 rows read')
    expect(refreshVacancyCandidatesAndLayout).toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: Header Drift Re-map pre-fills the current mapping and completes the import via the layout payload', async () => {
    let layoutPayload: string | undefined
    stubFetch(
      () => jsonResponse(formLayoutDto()),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          const form = init.body as FormData
          if (form.get('layout') === null) {
            return Promise.resolve(
              jsonResponse(
                {
                  title: 'Candidates.FormHeaderDrift',
                  detail: 'The uploaded form headers differ from the saved Form Layout.',
                  headers: ['Timestamp', 'Nama lengkap sesuai KTP', 'Email aktif', 'Pengalaman kerja'],
                  changes: [
                    { ordinal: 1, was: 'Nama Lengkap', now: 'Nama lengkap sesuai KTP' },
                  ],
                },
                409,
              ),
            )
          }
          layoutPayload = form.get('layout') as string
          return Promise.resolve(
            jsonResponse({
              rowsRead: 3,
              created: 1,
              updated: 2,
              skippedOutdated: 0,
              priorApplications: 0,
            }),
          )
        }
        return undefined
      },
    )

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Nama'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    dialogButton('Re-map…')?.click()
    await flushPromises()

    // The drift dialog swaps for the guided panel pre-filled with the current
    // mapping against the new file's headers — the label carries over.
    expect(document.body.textContent).not.toContain('Form headers changed')
    expect(document.body.textContent).toContain('Set up the form layout to finish importing')
    const nameSelect = Array.from(document.body.querySelectorAll('button')).find(
      (candidate) => candidate.getAttribute('aria-label') === 'Name',
    )
    expect(nameSelect?.textContent).toContain('1 · Nama lengkap sesuai KTP')

    dialogButton('Save layout & import')?.click()
    await flushPromises()

    expect(JSON.parse(layoutPayload!)).toEqual({
      columns: [
        { ordinal: 1, role: 'name', label: 'Candidate name' },
        { ordinal: 2, role: 'contactEmail', label: null },
        { ordinal: 3, role: null, label: null },
      ],
    })
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: expect.stringContaining('Form import complete:') }),
    )
    wrapper.unmount()
  })

  it('domain: Form Layout edits save through PUT and the toast states the back-fill', async () => {
    let putBody: { columns?: unknown } | undefined
    stubFetch(
      () => jsonResponse(formLayoutDto()),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/form-layout')) {
          putBody = JSON.parse(String(init.body))
          return Promise.resolve(
            jsonResponse(formLayoutDto({ candidatesUpdated: 2, typedOverridesKept: 1 })),
          )
        }
        return undefined
      },
    )

    const wrapper = mountConsole()
    await flushPromises()

    // The read-only summary shows the current mapping before anything opens.
    expect(wrapper.text()).toContain('2 · Email aktif')

    await openLayoutEdit(wrapper)

    // Edit mode pre-fills the saved mapping from the header snapshot.
    expect(document.body.textContent).toContain('Columns are matched by position')
    const nameSelect = Array.from(document.body.querySelectorAll('button')).find(
      (candidate) => candidate.getAttribute('aria-label') === 'Name',
    )
    expect(nameSelect?.textContent).toContain('1 · Nama Lengkap')

    dialogButton('Save layout')?.click()
    await flushPromises()

    expect(putBody).toEqual({
      columns: [
        { ordinal: 1, role: 'name', label: 'Candidate name' },
        { ordinal: 2, role: 'contactEmail', label: null },
        { ordinal: 3, role: null, label: null },
      ],
    })
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Form layout saved',
        description: 'Details re-filled for 2 candidates from the form answers · 1 typed override kept.',
        color: 'success',
      }),
    )
    // The panel closes on success.
    expect(document.body.textContent).not.toContain('Columns are matched by position')
    wrapper.unmount()
  })

  it('domain: a Form Layout is invalid without Name and Contact email bound', async () => {
    let putCalled = false
    stubFetch(
      () => jsonResponse(formLayoutDto()),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/form-layout')) {
          putCalled = true
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        return undefined
      },
    )

    const wrapper = mountConsole()
    await flushPromises()

    await openLayoutEdit(wrapper)

    // Un-binding Contact email makes the layout invalid.
    await pickRoleColumn('Contact email', 'Not assigned')

    dialogButton('Save layout')?.click()
    await flushPromises()

    // The save never leaves the client; the panel stays open with the reason.
    expect(putCalled).toBe(false)
    expect(document.body.textContent).toContain('Name and Contact email are required.')
    expect(document.body.textContent).toContain('Columns are matched by position')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('domain: Form Layout picks at most 8 columns, roles included', async () => {
    stubFetch(
      () =>
        jsonResponse(
          formLayoutDto({
            headerSnapshot: [
              'Timestamp',
              'Nama',
              'Email',
              'Satu',
              'Dua',
              'Tiga',
              'Empat',
              'Lima',
              'Enam',
              'Tujuh',
            ],
            columns: [
              { ordinal: 1, role: 'name', label: null },
              { ordinal: 2, role: 'contactemail', label: null },
              { ordinal: 3, role: null, label: null },
              { ordinal: 4, role: null, label: null },
              { ordinal: 5, role: null, label: null },
              { ordinal: 6, role: null, label: null },
              { ordinal: 7, role: null, label: null },
              { ordinal: 8, role: null, label: null },
            ],
          }),
        ),
      () => undefined,
    )

    const wrapper = mountConsole()
    await flushPromises()

    await openLayoutEdit(wrapper)

    // At the cap the counter becomes the explanation and the 9th pick is blocked at the control.
    expect(document.body.textContent).toContain('8 columns at most, roles included')
    const ninth = document.body.querySelector<HTMLButtonElement>(
      'button[role="checkbox"][aria-label="9 · Tujuh"]',
    )
    expect(ninth, 'the 9th column checkbox').not.toBeNull()
    expect(ninth!.disabled).toBe(true)
    wrapper.unmount()
  })

  it('domain: Cancel import in the guided Form Layout setup drops the held file', async () => {
    let postCount = 0
    stubFetch(noLayout, (url, init) => {
      if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
        postCount += 1
        return Promise.resolve(
          jsonResponse(
            {
              title: 'Candidates.FormLayoutRequired',
              detail: 'The vacancy needs a valid Form Layout first.',
              headers: ['Timestamp', 'Nama Lengkap', 'Email aktif', 'Pengalaman kerja'],
            },
            409,
          ),
        )
      }
      return undefined
    })

    const wrapper = mountConsole()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Nama'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()
    expect(document.body.textContent).toContain('Set up the form layout to finish importing')

    dialogButton('Cancel import')?.click()
    await flushPromises()

    // The held file is dropped: the panel closes, nothing else is sent, no toast.
    expect(document.body.textContent).not.toContain('Set up the form layout to finish importing')
    expect(postCount).toBe(1)
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })
})
