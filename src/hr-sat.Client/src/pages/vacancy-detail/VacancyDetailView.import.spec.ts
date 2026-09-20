import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bodyElement,
  candidateSummary,
  dialogButton,
  dropFiles,
  formLayoutDto,
  importedCandidate,
  jsonResponse,
  mountView,
  openImportDialog,
  pagedCandidates,
  pickRoleColumn,
  stubFetch,
  toastAdd,
  vacancyDetails,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

describe('VacancyDetailView · candidate import and Form Layout', () => {
  it('US-12: HR drops .eml files into a vacancy and sees each file’s outcome', async () => {
    let imported = false
    let sentFileNames: string[] | undefined
    stubFetch(
      () =>
        vacancyDetails({
          progress: imported
            ? { processedCandidates: 0, totalCandidates: 1 }
            : { processedCandidates: 0, totalCandidates: 0 },
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
          imported = true
          const form = init.body as FormData
          sentFileNames = form.getAll('files').map((entry) => (entry as File).name)
          return Promise.resolve(
            jsonResponse({
              results: [
                {
                  fileName: 'alice.eml',
                  status: 'imported',
                  error: null,
                  candidate: importedCandidate(1, 'alice.eml'),
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
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates(imported ? [candidateSummary(1)] : [])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('Welder')

    await openImportDialog(wrapper)
    await dropFiles([
      new File(['alice source'], 'alice.eml', { type: 'message/rfc822' }),
      new File(['broken source'], 'broken.eml', { type: 'message/rfc822' }),
    ])
    await flushPromises()

    // Every dropped file is submitted as a repeated multipart `files` entry.
    expect(sentFileNames).toEqual(['alice.eml', 'broken.eml'])

    // Each file's outcome is visible to HR.
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
    wrapper.unmount()
  })

  it('US-13: the vacancy progress reflects the imported candidates', async () => {
    let imported = false
    stubFetch(
      () =>
        vacancyDetails({
          progress: imported
            ? { processedCandidates: 0, totalCandidates: 1 }
            : { processedCandidates: 0, totalCandidates: 0 },
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
          imported = true
          return Promise.resolve(
            jsonResponse({
              results: [
                {
                  fileName: 'alice.eml',
                  status: 'imported',
                  error: null,
                  candidate: importedCandidate(1, 'alice.eml'),
                },
              ],
            }),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates(imported ? [candidateSummary(1)] : [])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const progress = () => wrapper.find('[aria-label="Vacancy progress"]').text()
    expect(progress()).toContain('0/0')

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    expect(progress()).toContain('0/1')
    wrapper.unmount()
  })

  it('US-12: HR is told when the import request fails', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import')) {
          return Promise.resolve(
            jsonResponse(
              { errors: { files: ['At least one .eml file is required.'] } },
              400,
            ),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    let imported = false
    let sentFileName: string | undefined
    stubFetch(
      () =>
        vacancyDetails({
          progress: imported
            ? { processedCandidates: 0, totalCandidates: 1 }
            : { processedCandidates: 0, totalCandidates: 0 },
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          imported = true
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
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates(imported ? [candidateSummary(1)] : [])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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

    // …and the candidate list refreshes behind it.
    expect(wrapper.text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('domain: a mixed drop of .eml and .csv files is rejected inline and nothing uploads', async () => {
    const posted: string[] = []
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST') {
          posted.push(url)
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST') {
          posted.push(url)
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          return Promise.resolve(
            jsonResponse({ errors: { file: ['Row 12 has 7 cells but the header has 9.'] } }, 400),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          return Promise.resolve(jsonResponse({ title: 'IntakeRounds.Closed' }, 409))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          return Promise.resolve(
            jsonResponse(
              { title: 'Candidates.FormImportLifecycleConflict', detail: 'The vacancy is invalid.' },
              409,
            ),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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

  it('domain: a re-uploaded export stacks the Resubmitted badge on the affected candidate', async () => {
    let imported = false
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/candidates/import-form')) {
          imported = true
          return Promise.resolve(
            jsonResponse({
              rowsRead: 3,
              created: 0,
              updated: 3,
              skippedOutdated: 0,
              priorApplications: 0,
            }),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse(pagedCandidates(imported ? [candidateSummary(1, { isResubmitted: true })] : [])),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await openImportDialog(wrapper)
    await dropFiles([new File(['Timestamp,Email'], 'responses.csv', { type: 'text/csv' })])
    await flushPromises()

    // Zero-count segments drop out of the summary line.
    expect(document.body.querySelector('[role="status"]')?.textContent).toContain(
      '3 rows read · 3 updated (resubmitted)',
    )

    const aliceRow = wrapper.findAll('.crow').find((row) => row.text().includes('Alice Applicant'))
    expect(aliceRow?.text()).toContain('Resubmitted')
    wrapper.unmount()
  })

  it('domain: the first .csv upload routes into the guided Form Layout setup and completes the import', async () => {
    let imported = false
    let layoutPayload: string | undefined
    let sentFileName: string | undefined
    stubFetch(
      () => vacancyDetails(),
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
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(
            imported
              ? jsonResponse(formLayoutDto())
              : jsonResponse({ title: 'FormLayouts.NotFound', detail: 'No form layout.' }, 404),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates(imported ? [candidateSummary(1)] : [])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    // …the stamped layout shows in the summary card…
    expect(wrapper.text()).toContain('1 · Nama Lengkap')
    // …and the imported candidates are listed.
    expect(wrapper.text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('domain: Header Drift pauses a re-upload behind the drift dialog; Confirm imports with confirmDrift', async () => {
    const confirmDriftValues: (FormDataEntryValue | null)[] = []
    stubFetch(
      () => vacancyDetails(),
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
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
    wrapper.unmount()
  })

  it('domain: Header Drift Re-map pre-fills the current mapping and completes the import via the layout payload', async () => {
    let layoutPayload: string | undefined
    stubFetch(
      () => vacancyDetails(),
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
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/form-layout')) {
          putBody = JSON.parse(String(init.body))
          return Promise.resolve(
            jsonResponse(formLayoutDto({ candidatesUpdated: 2, typedOverridesKept: 1 })),
          )
        }
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // The read-only summary shows the current mapping before anything opens.
    expect(wrapper.text()).toContain('2 · Email aktif')

    const layoutButton = wrapper
      .findAll('button')
      .find((candidate) => candidate.text().includes('Form layout'))
    expect(layoutButton, 'a Form layout header button').toBeDefined()
    await layoutButton!.trigger('click')
    await flushPromises()

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
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/form-layout')) {
          putCalled = true
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(jsonResponse(formLayoutDto()))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const layoutButton = wrapper
      .findAll('button')
      .find((candidate) => candidate.text().includes('Form layout'))
    await layoutButton!.trigger('click')
    await flushPromises()

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
      () => vacancyDetails(),
      (url, _init) => {
        if (url.endsWith('/form-layout')) {
          return Promise.resolve(
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
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const layoutButton = wrapper
      .findAll('button')
      .find((candidate) => candidate.text().includes('Form layout'))
    await layoutButton!.trigger('click')
    await flushPromises()

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
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
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
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse(pagedCandidates([])))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
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
