import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bobSummary,
  candidateSummary,
  dialogButton,
  dropFiles,
  importedCandidate,
  jsonResponse,
  mountView,
  mountViewWithQuery,
  openImportDialog,
  stubFetch,
  toastAdd,
  vacancyDetails,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

describe('VacancyDetailView · candidate list', () => {
  it('US-14: HR sees the whole pipeline at a glance — name, notes, and status per candidate', async () => {
    stubFetch(
      () =>
        vacancyDetails({
          requirements: [
            { id: 11, phrase: 'MIG welding', position: 0 },
            { id: 12, phrase: 'TIG welding', position: 1 },
          ],
          progress: { processedCandidates: 1, totalCandidates: 3 },
        }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              bobSummary({ contactEmail: 'bob@example.com' }),
              candidateSummary(3, {
                sourceSenderName: null,
                sourceSenderEmail: null,
                sourceSubject: 'CV submission via web form',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // Header: vacancy title with date and the email-template action.
    expect(wrapper.text()).toContain('Welder')
    expect(wrapper.text()).toContain('Opened')
    const sendAll = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Send email to all candidates'))
    expect(sendAll).toBeDefined()
    expect((sendAll!.element as HTMLButtonElement).disabled).toBe(false)

    // Column order per the S3 sketch: Candidate | Received | CV | Notes | Review status | Actions.
    const headerCells = wrapper.findAll('.ctable__head th')
    expect(headerCells.map((cell) => cell.text())).toEqual([
      'Candidate',
      'Received',
      'CV',
      'Notes',
      'Review status',
      'Actions',
    ])
    expect(wrapper.find('.ctable__head').text()).not.toContain('Match Status')

    // Display name falls back to the sender, then to the email subject.
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).toContain('CV submission via web form')
    expect(wrapper.find('[aria-label="Skills requirements"]').text()).toContain('MIG welding')
    expect(wrapper.find('[aria-label="Skills requirements"]').text()).toContain('TIG welding')
    expect(wrapper.text()).toContain('New')
    expect(wrapper.text()).toContain('Shortlisted')
    expect(wrapper.text()).toContain('Strong MIG experience')
    expect(wrapper.text()).not.toContain('Extraction pending')

    // Row send follows the Contactable Candidate rule: a Bench candidate with an
    // email recorded can be contacted; undecided (new) rows are disabled and the
    // label carries the reason. Delete stays available regardless.
    const sendButtonIn = (name: string) =>
      wrapper
        .findAll('.crow')
        .find((row) => row.text().includes(name))!
        .find('button[aria-label*="Send email"]')
    const bobSend = sendButtonIn('Bob Builder')
    expect(bobSend.attributes('aria-label')).toBe('Send email')
    expect((bobSend.element as HTMLButtonElement).disabled).toBe(false)
    const aliceSend = sendButtonIn('Alice Applicant')
    expect((aliceSend.element as HTMLButtonElement).disabled).toBe(true)
    expect(aliceSend.attributes('aria-label')).toBe('Send email — Not yet shortlisted or rejected')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(3)

    // The drop zone only lives inside the import dialog, which starts closed.
    expect(wrapper.find('.dropzone').exists()).toBe(false)
    expect(document.body.querySelector('.dropzone')).toBeNull()
    wrapper.unmount()
  })

  it('domain: Needed Hires stay visible in the vacancy rollup and candidate workspace', async () => {
    stubFetch(
      () => vacancyDetails({ hiring: { neededHires: 4, activeHires: 1 } }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const hiringPlan = wrapper.find('[aria-label="Hiring plan"]')
    expect(hiringPlan.text()).toContain('Needed hires')
    expect(hiringPlan.text()).toContain('Active hires')
    expect(hiringPlan.text()).toContain('Still needed')
    expect(hiringPlan.text()).toContain('4')
    expect(hiringPlan.text()).toContain('1')
    expect(hiringPlan.text()).toContain('3')

    const workspaceHiringPlan = wrapper.find('[aria-label="Candidate workspace hiring plan"]')
    expect(workspaceHiringPlan.text()).toContain('1/4')
    expect(workspaceHiringPlan.text()).toContain('3 to go')
    expect(workspaceHiringPlan.text()).not.toContain('Filled')
    wrapper.unmount()
  })

  it('US-14: HR deletes a candidate after confirming', async () => {
    let deleted = false
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'DELETE' && url.includes('/rounds/1/candidates/2')) {
          deleted = true
          return Promise.resolve(new Response(null, { status: 204 }))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse(deleted ? [candidateSummary(1)] : [candidateSummary(1), bobSummary()]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    expect(bobRow).toBeDefined()
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    // The confirm dialog names the candidate being deleted.
    expect(document.body.textContent).toContain('Bob Builder')
    expect(document.body.textContent).toContain("can't be undone")

    dialogButton('Delete candidate')?.click()
    await flushPromises()

    expect(deleted).toBe(true)
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Candidate "Bob Builder" deleted successfully',
        color: 'success',
      }),
    )
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.text()).not.toContain('Bob Builder')
    wrapper.unmount()
  })

  it('US-14: HR is told when deleting a candidate fails and the list stays intact', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url, init) => {
        if (init?.method === 'DELETE' && url.includes('/rounds/1/candidates/2')) {
          return Promise.resolve(jsonResponse({ title: 'Server error' }, 500))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    dialogButton('Delete candidate')?.click()
    await flushPromises()

    // Dialog stays open with the error; the loaded list is untouched.
    expect(document.body.textContent).toContain('Something went wrong')
    expect(document.body.textContent).toContain('Please try again.')
    expect(document.body.textContent).toContain("can't be undone")
    expect(wrapper.text()).toContain('Bob Builder')
    expect(toastAdd).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('US-12/US-14: a successful import replaces the empty state with the candidate list', async () => {
    let imported = false
    stubFetch(
      () => vacancyDetails(),
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
          return Promise.resolve(jsonResponse(imported ? [candidateSummary(1)] : []))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No candidates yet')
    expect(wrapper.find('.crow').exists()).toBe(false)

    await openImportDialog(wrapper)
    await dropFiles([new File(['alice source'], 'alice.eml', { type: 'message/rfc822' })])
    await flushPromises()

    expect(wrapper.text()).not.toContain('No candidates yet')
    expect(wrapper.text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('domain: closed vacancy is read-only — no import is offered', async () => {
    stubFetch(
      () => vacancyDetails({ status: 'closed', closedAt: '2026-08-29T00:00:00Z' }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Closed')
    expect(wrapper.text()).toContain('read-only')
    expect(
      wrapper.findAll('button').filter((button) => button.text().includes('Import')),
    ).toHaveLength(0)
    expect(wrapper.find('input[type="file"]').exists()).toBe(false)
    expect(document.body.querySelector('.dropzone')).toBeNull()
    wrapper.unmount()
  })

  it('domain: closed vacancy is read-only — candidate rows have no actions', async () => {
    stubFetch(
      () =>
        vacancyDetails({
          status: 'closed',
          closedAt: '2026-08-29T00:00:00Z',
          progress: { processedCandidates: 0, totalCandidates: 1 },
        }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)
    expect(wrapper.findAll('button[aria-label*="Send email"]')).toHaveLength(0)
    wrapper.unmount()
  })

  it('domain: HR closes a vacancy and it becomes read-only', async () => {
    let closed = false
    stubFetch(
      () =>
        vacancyDetails(
          closed
            ? { status: 'closed', closedAt: '2026-09-15T00:00:00Z' }
            : {},
        ),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/vacancies/1/close')) {
          closed = true
          return Promise.resolve(
            jsonResponse(vacancyDetails({ status: 'closed', closedAt: '2026-09-15T00:00:00Z' })),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // Open vacancy offers the close affordance.
    const closeButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Close vacancy'))
    expect(closeButton, 'close affordance on an open vacancy').toBeDefined()
    await closeButton!.trigger('click')
    await flushPromises()

    // The confirm spells out what closing means.
    expect(document.body.textContent).toContain('Close vacancy')
    expect(document.body.textContent).toContain('read-only')

    dialogButton('Close vacancy')?.click()
    await flushPromises()

    // After closing, the vacancy reads closed and the affordance is gone.
    expect(closed).toBe(true)
    expect(wrapper.text()).toContain('Closed')
    expect(
      wrapper.findAll('button').filter((button) => button.text().includes('Close vacancy')),
    ).toHaveLength(0)
    wrapper.unmount()
  })

  it('US-14: HR filters by review-status chips with live counts, combined AND with search', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              bobSummary(),
              candidateSummary(3, {
                sourceSenderName: 'Carol Welder',
                sourceSenderEmail: 'carol@example.com',
                reviewStatus: 'shortlisted',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const chips = () => wrapper.findAll('[aria-label="Filter by review status"] button')

    // Live counts per chip; the full list shows before any filter is active.
    expect(chips().map((chip) => chip.text().replace(/\s+/g, ' ').trim())).toEqual([
      'All 3',
      'New 1',
      'Flagged 0',
      'Shortlisted 2',
      'Rejected 0',
    ])
    expect(wrapper.findAll('.crow')).toHaveLength(3)

    // Only the active chip is highlighted.
    await chips()[3]!.trigger('click')
    expect(wrapper.findAll('.crow')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('Alice Applicant')
    expect(chips()[3]!.attributes('aria-pressed')).toBe('true')
    expect(chips()[0]!.attributes('aria-pressed')).toBe('false')

    // Search narrows the filtered list further (AND semantics).
    await wrapper.find('input[aria-label="Search candidates"]').setValue('bob')
    const rows = wrapper.findAll('.crow')
    expect(rows).toHaveLength(1)
    expect(rows[0]!.text()).toContain('Bob Builder')

    // No match under the active filters shows the shared filter-empty state.
    await wrapper.find('input[aria-label="Search candidates"]').setValue('alice')
    expect(wrapper.findAll('.crow')).toHaveLength(0)
    expect(wrapper.text()).toContain('No candidates match')
    expect(wrapper.text()).not.toContain('No candidates yet')

    // Clear filters restores the full list and resets the search input.
    const clearButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Clear filters'))
    await clearButton!.trigger('click')
    expect(wrapper.findAll('.crow')).toHaveLength(3)
    expect(
      (wrapper.find('input[aria-label="Search candidates"]').element as HTMLInputElement).value,
    ).toBe('')
    expect(chips()[0]!.attributes('aria-pressed')).toBe('true')
    wrapper.unmount()
  })

  it('US-14: outcome chips nest under Shortlisted and hide for other statuses', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const outcomeGroup = () => wrapper.find('[aria-label="Filter by hire outcome"]')
    const statusChips = () => wrapper.findAll('[aria-label="Filter by review status"] button')

    // All: outcome chips visible (shortlisted candidates are part of the list).
    expect(outcomeGroup().exists()).toBe(true)

    // Shortlisted: outcome chips stay visible.
    await statusChips()[3]!.trigger('click')
    expect(outcomeGroup().exists()).toBe(true)

    // New: outcome chips hide — the facet does not exist outside Shortlisted.
    await statusChips()[1]!.trigger('click')
    expect(outcomeGroup().exists()).toBe(false)

    // Back to All: outcome chips return.
    await statusChips()[0]!.trigger('click')
    expect(outcomeGroup().exists()).toBe(true)
    wrapper.unmount()
  })

  it('US-14: a status that excludes shortlisted sanitizes a stale outcome from the URL', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              candidateSummary(2, {
                sourceSenderName: 'Carol Welder',
                sourceSenderEmail: 'carol@example.com',
                reviewStatus: 'shortlisted',
                hireOutcome: 'hired',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    // Stale link: status=new with outcome=hired is a dead combination.
    const { wrapper } = await mountViewWithQuery('1', { status: 'new', outcome: 'hired' })
    await flushPromises()

    // The outcome filter falls back to 'any', so the New candidate still shows.
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('Alice Applicant')
    // Outcome chips stay hidden under New.
    expect(wrapper.find('[aria-label="Filter by hire outcome"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('US-14: HR searches candidates by sender email and email subject', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              bobSummary(),
              candidateSummary(3, {
                sourceSenderName: null,
                sourceSenderEmail: null,
                sourceSubject: 'CV submission via web form',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const search = wrapper.find('input[aria-label="Search candidates"]')

    await search.setValue('bob@example.com')
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).not.toContain('Alice Applicant')

    await search.setValue('web form')
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('CV submission via web form')

    await search.setValue('')
    expect(wrapper.findAll('.crow')).toHaveLength(3)
    wrapper.unmount()
  })

  it('domain: the Bench outcome facet scopes to shortlisted candidates and composes with search', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              bobSummary(),
              candidateSummary(3, {
                sourceSenderName: 'Carol Hired',
                reviewStatus: 'shortlisted',
                hireOutcome: 'hired',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const outcomeChips = () => wrapper.findAll('[aria-label="Filter by hire outcome"] button')
    expect(outcomeChips().map((chip) => chip.text().replace(/\s+/g, ' ').trim())).toEqual([
      'Any outcome 3',
      'Bench 1',
      'Hired 1',
      'Runaway 0',
      'Declined 0',
    ])

    await outcomeChips()[1]!.trigger('click')
    expect(wrapper.findAll('.crow')).toHaveLength(1)
    expect(wrapper.text()).toContain('Bob Builder')
    expect(wrapper.text()).not.toContain('Carol Hired')

    await wrapper.find('input[aria-label="Search candidates"]').setValue('alice')
    expect(wrapper.findAll('.crow')).toHaveLength(0)
    expect(wrapper.text()).toContain('No candidates match these filters')

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Clear filters'))
      ?.trigger('click')
    expect(outcomeChips()[0]!.attributes('aria-pressed')).toBe('true')
    expect(wrapper.findAll('.crow')).toHaveLength(3)
    wrapper.unmount()
  })

  it('US-14: HR sorts by received date, newest first by default, toggling to oldest', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1), // 2026-08-28
              candidateSummary(2, {
                sourceSenderName: 'Bob Builder',
                sourceSentAt: '2026-08-30T15:30:00Z',
              }),
              candidateSummary(3, {
                sourceSenderName: 'Carol Welder',
                sourceSentAt: '2026-08-25T08:00:00Z',
              }),
              candidateSummary(4, {
                sourceSenderName: 'Dave NoDate',
                sourceSentAt: null,
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const names = () => wrapper.findAll('.crow__name-text').map((cell) => cell.text())
    const sortHeader = () => wrapper.find('th[aria-sort]')

    // Newest first by default; a missing sent-at sinks to the bottom.
    expect(sortHeader().attributes('aria-sort')).toBe('descending')
    expect(names()).toEqual(['Bob Builder', 'Alice Applicant', 'Carol Welder', 'Dave NoDate'])

    await sortHeader().find('button').trigger('click')
    expect(sortHeader().attributes('aria-sort')).toBe('ascending')
    expect(names()).toEqual(['Carol Welder', 'Alice Applicant', 'Bob Builder', 'Dave NoDate'])

    await sortHeader().find('button').trigger('click')
    expect(sortHeader().attributes('aria-sort')).toBe('descending')
    expect(names()).toEqual(['Bob Builder', 'Alice Applicant', 'Carol Welder', 'Dave NoDate'])
    wrapper.unmount()
  })

  it('US-14: the CV column shows a paperclip when documents exist and a No CV badge otherwise', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1),
              candidateSummary(2, { sourceSenderName: 'Bob Builder', cvDocumentCount: 2 }),
              candidateSummary(3, { sourceSenderName: 'Carol NoCv', cvDocumentCount: 0 }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const cvCells = wrapper.findAll('.crow__cv')
    expect(cvCells).toHaveLength(3)
    expect(cvCells[0]!.text()).toContain('1 CV document')
    expect(cvCells[1]!.text()).toContain('2 CV documents')
    expect(cvCells[2]!.text()).toContain('No CV')
    expect(cvCells[2]!.text()).not.toContain('CV document')
    wrapper.unmount()
  })

  it('US-14: HR opens the review workspace by clicking a candidate row', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('candidate-review')
    expect(router.currentRoute.value.fullPath).toBe('/vacancies/1/rounds/1/review/2')
    wrapper.unmount()
  })

  it('US-17: HR opens review with the active candidate filters and sort', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(
            jsonResponse([
              candidateSummary(1, {
                sourceSenderName: 'Alice Applicant',
                reviewStatus: 'shortlisted',
                sourceSentAt: '2026-08-30T09:00:00Z',
              }),
              candidateSummary(2, {
                sourceSenderName: 'Bob Builder',
                reviewStatus: 'shortlisted',
                hireOutcome: 'hired',
                sourceSentAt: '2026-08-20T09:00:00Z',
              }),
              candidateSummary(3, {
                sourceSenderName: 'Carol Welder',
                sourceSentAt: '2026-08-10T09:00:00Z',
              }),
            ]),
          )
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    const shortlistChip = wrapper
      .findAll('[aria-label="Filter by review status"] button')
      .find((button) => button.text().includes('Shortlisted'))
    await shortlistChip!.trigger('click')
    await wrapper
      .findAll('[aria-label="Filter by hire outcome"] button')
      .find((button) => button.text().includes('Hired'))
      ?.trigger('click')
    await wrapper.find('th[aria-sort] button').trigger('click')

    await wrapper.findAll('.crow')[0]!.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe(
      '/vacancies/1/rounds/1/review/2?status=shortlisted&outcome=hired&sort=oldest',
    )
    wrapper.unmount()
  })

  it('US-14: row action buttons do not navigate away from the list', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1), bobSummary()]))
        }
        return undefined
      },
    )

    const { wrapper, router } = mountView()
    await flushPromises()

    const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
    await bobRow!.find('button[aria-label="Delete candidate"]').trigger('click')
    await flushPromises()

    // The delete dialog opened instead of a navigation.
    expect(document.body.textContent).toContain("can't be undone")
    expect(router.currentRoute.value.fullPath).toBe('/')
    wrapper.unmount()
  })
})
