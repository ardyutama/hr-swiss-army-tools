import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  candidateSummary,
  dropFiles,
  formLayoutDto,
  importedCandidate,
  jsonResponse,
  mountView,
  openImportDialog,
  pagedCandidates,
  stubFetch,
  toastAdd,
  vacancyDetails,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

// Page-level smoke for the import console seam. The dialog, refusal, drift,
// and layout scenarios live with the console itself
// (src/features/vacancy-detail/importConsole.spec.ts); what stays here is what
// only the page can show: the import entry point, the progress bar and the
// candidate list reacting to a completed import, and the layout summary's
// edit entry point.
describe('VacancyDetailView · candidate import and Form Layout', () => {
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

  it('domain: the Form Layout summary card opens the layout editor pre-filled with the saved mapping', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
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

    // The read-only summary shows the current mapping…
    expect(wrapper.text()).toContain('2 · Email aktif')

    // …and its Edit action opens the layout editor against the saved snapshot.
    const editButton = wrapper
      .findAll('button')
      .find((candidate) => candidate.text().includes('Edit layout'))
    expect(editButton, 'the summary card’s Edit layout button').toBeDefined()
    await editButton!.trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Columns are matched by position')
    const nameSelect = Array.from(document.body.querySelectorAll('button')).find(
      (candidate) => candidate.getAttribute('aria-label') === 'Name',
    )
    expect(nameSelect?.textContent).toContain('1 · Nama Lengkap')
    wrapper.unmount()
  })
})
