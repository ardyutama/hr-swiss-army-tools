import { DOMWrapper, flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bobSummary,
  candidateSummary,
  closedRound,
  dialogButton,
  jsonResponse,
  mountView,
  openRound,
  stubFetch,
  toastAdd,
  vacancyDetails,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

describe('VacancyDetailView · intake rounds and promotion', () => {
  it('domain: a single open round shows no round chrome — the V1 look stays', async () => {
    stubFetch(
      () => vacancyDetails(),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // No round list or round labels: a lone active round keeps the V1 layout.
    expect(wrapper.find('[aria-label="Intake rounds"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Round 1')
    // The lifecycle has a compact entry point without exposing round chrome by default.
    expect(
      wrapper.findAll('button').some((button) => button.text().includes('Manage rounds')),
    ).toBe(true)
    expect(
      wrapper.findAll('button').some((button) => button.text().includes('Promote from')),
    ).toBe(false)
    // The V1 flow is otherwise untouched: import affordance and the round's candidates.
    expect(
      wrapper.findAll('button').some((button) => button.text().includes('Import candidates')),
    ).toBe(true)
    expect(wrapper.text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('Promote: HR moves eligible candidates from a closed round into the active round', async () => {
    let promoted = false
    let activeListRequests = 0
    let promotionBody: unknown
    const activeCandidate = candidateSummary(3, { sourceSenderName: 'Current Applicant' })
    const latestRoundCandidates = [
      candidateSummary(1, { sourceSenderName: 'Alice Applicant', reviewStatus: 'new' }),
      candidateSummary(2, { sourceSenderName: 'Bob Flagged', reviewStatus: 'flagged' }),
      candidateSummary(4, { sourceSenderName: 'Rejected Applicant', reviewStatus: 'rejected' }),
      candidateSummary(5, {
        sourceSenderName: 'Hired Applicant',
        reviewStatus: 'shortlisted',
        hireOutcome: 'hired',
      }),
      candidateSummary(6, {
        sourceSenderName: 'Declined Applicant',
        reviewStatus: 'shortlisted',
        hireOutcome: 'declined',
      }),
    ]
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({
              id: 1,
              roundNumber: 1,
              name: 'First wave',
              closedAt: '2026-09-10T10:00:00Z',
              candidateCount: 1,
            }),
            closedRound({
              id: 2,
              roundNumber: 2,
              name: 'Second wave',
              closedAt: '2026-09-08T10:00:00Z',
              candidateCount: promoted ? 0 : 5,
            }),
            openRound({ id: 3, roundNumber: 3, candidateCount: promoted ? 2 : 1 }),
          ],
          progress: {
            processedCandidates: promoted ? 1 : 0,
            totalCandidates: promoted ? 2 : 1,
          },
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/rounds/3/promotions')) {
          promoted = true
          promotionBody = JSON.parse(String(init.body))
          return Promise.resolve(jsonResponse([candidateSummary(7, { sourceSenderName: 'Earlier Applicant' })]))
        }
        if (url.includes('/rounds/3/candidates')) {
          activeListRequests += 1
          return Promise.resolve(
            jsonResponse(promoted ? [activeCandidate, candidateSummary(7, { sourceSenderName: 'Earlier Applicant' })] : [activeCandidate]),
          )
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse(latestRoundCandidates))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(8, { sourceSenderName: 'Oldest Applicant' })]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const promoteButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Promote from'))
    expect(promoteButton, 'a Promote from action for the active round').toBeDefined()
    await promoteButton!.trigger('click')
    await flushPromises()

    const dialog = document.body.querySelector('[role="dialog"]')
    expect(dialog?.textContent).toContain('Round 1 — First wave')
    expect(dialog?.textContent).toContain('Oldest Applicant')
    expect(dialog?.textContent).not.toContain('Alice Applicant')

    const sourceRoundTrigger = document.body.querySelector('[aria-label="Source round"]')
    expect(sourceRoundTrigger?.tagName).toBe('BUTTON')
    await new DOMWrapper(sourceRoundTrigger as HTMLButtonElement).trigger('keydown', {
      key: 'ArrowDown',
    })
    await flushPromises()

    const sourceRoundOption = Array.from(document.body.querySelectorAll('[role="option"]')).find(
      (option) => option.textContent?.includes('Round 2 — Second wave'),
    )
    expect(sourceRoundOption).toBeDefined()
    await new DOMWrapper(sourceRoundOption as HTMLElement).trigger('keydown', { key: 'Enter' })
    await flushPromises()
    expect(document.body.textContent).toContain('Alice Applicant')
    expect(document.body.textContent).toContain('Bob Flagged')
    expect(document.body.textContent).not.toContain('Rejected Applicant')
    expect(document.body.textContent).not.toContain('Hired Applicant')
    expect(document.body.textContent).not.toContain('Declined Applicant')

    const candidateCheckbox = new DOMWrapper(
      document.body.querySelector('input[aria-label="Promote Alice Applicant"]') as HTMLInputElement,
    )
    await candidateCheckbox.setValue(true)
    await flushPromises()

    const submitButton = Array.from(document.body.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Promote selected'),
    )
    expect(submitButton).toBeDefined()
    await new DOMWrapper(submitButton as HTMLButtonElement).trigger('click')
    await flushPromises()

    expect(promotionBody).toEqual({ sourceRoundId: 2, candidateIds: [1] })
    expect(activeListRequests).toBeGreaterThan(1)
    expect(wrapper.text()).toContain('Earlier Applicant')
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: '1 candidate promoted successfully', color: 'success' }),
    )
    expect(document.body.querySelector('[role="dialog"]')).toBeNull()
    wrapper.unmount()
  })

  it('domain: HR can close the sole round and open the next round', async () => {
    let closed = false
    let opened = false
    let sentBody: unknown
    stubFetch(
      () =>
        vacancyDetails({
          hiring: { neededHires: 3, activeHires: 0 },
          rounds: opened
            ? [
                closedRound({ id: 1, roundNumber: 1, candidateCount: 1 }),
                openRound({ id: 2, roundNumber: 2, name: 'Second wave', candidateCount: 0 }),
              ]
            : [
                closed
                  ? closedRound({ id: 1, roundNumber: 1, candidateCount: 1 })
                  : openRound({ id: 1, roundNumber: 1, candidateCount: 1 }),
              ],
          progress: { processedCandidates: 0, totalCandidates: 1 },
        }),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/rounds/1/close')) {
          closed = true
          return Promise.resolve(
            jsonResponse({
              id: 1,
              vacancyId: 1,
              roundNumber: 1,
              name: null,
              status: 'closed',
              closedAt: '2026-09-07T10:00:00Z',
              candidateCount: 1,
            }),
          )
        }
        if (init?.method === 'POST' && url.endsWith('/vacancies/1/rounds')) {
          opened = true
          sentBody = JSON.parse(String(init.body))
          return Promise.resolve(
            jsonResponse(
              {
                id: 2,
                vacancyId: 1,
                roundNumber: 2,
                name: 'Second wave',
                status: 'open',
                closedAt: null,
                candidateCount: 0,
              },
              201,
            ),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    expect(wrapper.find('[aria-label="Intake rounds"]').exists()).toBe(false)
    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Manage rounds'))
      ?.trigger('click')
    await flushPromises()

    expect(wrapper.find('[aria-label="Intake rounds"]').text()).toContain('Round 1')
    await wrapper.find('button[aria-label="Close Round 1"]').trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('Closing is permanent')
    expect(document.body.textContent).toContain('3 slots still open')
    expect(document.body.textContent).toContain(
      'To keep hiring, open a new round after closing this one.',
    )

    dialogButton('Close round')?.click()
    await flushPromises()

    expect(closed).toBe(true)
    expect(wrapper.find('[aria-label="Intake rounds"]').text()).toContain('Closed')
    expect(wrapper.text()).toContain('Alice Applicant')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)

    const newRoundButton = wrapper
      .find('[aria-label="Intake rounds"]')
      .findAll('button')
      .find((button) => button.text().includes('New round'))
    expect(newRoundButton, 'a New round action after closing the sole round').toBeDefined()
    await newRoundButton!.trigger('click')
    await flushPromises()

    const nameInput = document.body.querySelector('input[aria-label="Round name"]')
    await new DOMWrapper(nameInput as HTMLInputElement).setValue('Second wave')
    dialogButton('Open round')?.click()
    await flushPromises()

    expect(opened).toBe(true)
    expect(sentBody).toEqual({ name: 'Second wave' })
    expect(wrapper.find('[aria-label="Intake rounds"]').text()).toContain('Round 2 — Second wave')
    expect(wrapper.find('[aria-current="true"]').text()).toContain('Round 2')
    expect(wrapper.text()).toContain('No candidates yet')
    wrapper.unmount()
  })

  it.each([
    { label: 'without a hiring target', hiring: null },
    { label: 'when the hiring target is already filled', hiring: { neededHires: 2, activeHires: 2 } },
  ])('domain: closing a round keeps the plain confirmation $label', async ({ hiring }) => {
    stubFetch(
      () => vacancyDetails({ hiring }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Manage rounds'))
      ?.trigger('click')
    await flushPromises()
    await wrapper.find('button[aria-label="Close Round 1"]').trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('Closing is permanent')
    expect(document.body.textContent).not.toContain('slots still open')
    wrapper.unmount()
  })

  it('domain: a second round opens the round list, pinned to the active round', async () => {
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, name: 'First wave', candidateCount: 2 }),
            openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
          ],
          progress: { processedCandidates: 0, totalCandidates: 1 },
        }),
      (url) => {
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // The round list shows every wave in order with its status and headcount.
    const section = wrapper.find('[aria-label="Intake rounds"]')
    expect(section.exists()).toBe(true)
    expect(section.text()).toContain('Round 1 — First wave')
    expect(section.text()).toContain('Round 2')
    expect(section.text()).toContain('Closed')
    expect(section.text()).toContain('Open')
    expect(section.text()).toContain('2 candidates')
    expect(section.text()).toContain('1 candidate')

    // The active round is selected, so its candidates are on screen.
    expect(section.find('[aria-current="true"]').text()).toContain('Round 2')
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Alice Applicant')
    wrapper.unmount()
  })

  it('domain: switching back to a visited round keeps its candidates responsive during refresh', async () => {
    let roundTwoRequests = 0
    let resolveRoundTwoRefresh: ((response: Response) => void) | undefined
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, candidateCount: 1 }),
            openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
          ],
          progress: { processedCandidates: 0, totalCandidates: 2 },
        }),
      (url) => {
        if (url.includes('/rounds/2/candidates')) {
          roundTwoRequests += 1
          if (roundTwoRequests === 1) {
            return Promise.resolve(jsonResponse([candidateSummary(1)]))
          }
          return new Promise<Response>((resolve) => {
            resolveRoundTwoRefresh = resolve
          })
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([bobSummary()]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const section = wrapper.find('[aria-label="Intake rounds"]')
    const roundOne = section
      .findAll('button')
      .find((button) => button.text().includes('Round 1'))
    const roundTwo = section
      .findAll('button')
      .find((button) => button.text().includes('Round 2'))

    await roundOne!.trigger('click')
    await flushPromises()
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Bob Builder')

    await roundTwo!.trigger('click')

    expect(section.find('[aria-current="true"]').text()).toContain('Round 2')
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Alice Applicant')
    expect(wrapper.find('[aria-label="Loading candidates"]').exists()).toBe(false)

    resolveRoundTwoRefresh?.(jsonResponse([candidateSummary(1)]))
    await flushPromises()
    wrapper.unmount()
  })

  it('domain: Round Management re-pins to the active round when the selected round disappears', async () => {
    let reloaded = false
    stubFetch(
      () =>
        vacancyDetails({
          rounds: reloaded
            ? [
                closedRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
                openRound({
                  id: 3,
                  roundNumber: 3,
                  name: 'Replacement wave',
                  candidateCount: 1,
                }),
              ]
            : [
                closedRound({ id: 1, roundNumber: 1, name: 'First wave', candidateCount: 1 }),
                openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
              ],
          progress: { processedCandidates: 0, totalCandidates: 1 },
        }),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/rounds/2/close')) {
          reloaded = true
          return Promise.resolve(
            jsonResponse({
              id: 2,
              vacancyId: 1,
              roundNumber: 2,
              name: null,
              status: 'closed',
              closedAt: '2026-09-09T10:00:00Z',
              candidateCount: 1,
            }),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([bobSummary()]))
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        if (url.includes('/rounds/3/candidates')) {
          return Promise.resolve(
            jsonResponse([candidateSummary(3, { sourceSenderName: 'Carol Welder' })]),
          )
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const section = wrapper.find('[aria-label="Intake rounds"]')
    const roundOne = section
      .findAll('button')
      .find((button) => button.text().includes('Round 1'))
    await roundOne!.trigger('click')
    await flushPromises()

    expect(section.find('[aria-current="true"]').text()).toContain('Round 1')
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Bob Builder')

    await wrapper.find('button[aria-label="Close Round 2"]').trigger('click')
    await flushPromises()
    dialogButton('Close round')?.click()
    await flushPromises()

    expect(reloaded).toBe(true)
    expect(wrapper.find('[aria-label="Intake rounds"]').text()).toContain(
      'Round 3 — Replacement wave',
    )
    expect(
      wrapper.find('[aria-label="Intake rounds"]').find('[aria-current="true"]').text(),
    ).toContain('Round 3')
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Carol Welder')
    expect(wrapper.find('[aria-label="Candidates"]').text()).not.toContain('Bob Builder')
    wrapper.unmount()
  })

  it('domain: a closed round shows its candidates read-only', async () => {
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, candidateCount: 1 }),
            openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
          ],
          progress: { processedCandidates: 0, totalCandidates: 2 },
        }),
      (url) => {
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([bobSummary()]))
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // Default view: the active round's candidates with their actions.
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Alice Applicant')
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(1)

    // HR opens the closed round to look back at its wave.
    const roundOne = wrapper
      .find('[aria-label="Intake rounds"]')
      .findAll('button')
      .find((button) => button.text().includes('Round 1'))
    await roundOne!.trigger('click')
    await flushPromises()

    const candidates = wrapper.find('[aria-label="Candidates"]')
    expect(candidates.text()).toContain('Bob Builder')
    expect(candidates.text()).not.toContain('Alice Applicant')
    expect(
      wrapper.find('[aria-label="Intake rounds"]').find('[aria-current="true"]').text(),
    ).toContain('Round 1')

    // Read-only: no delete or import affordances while the closed round is viewed.
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)
    expect(
      wrapper.findAll('button').filter((button) => button.text().includes('Import candidates')),
    ).toHaveLength(0)
    wrapper.unmount()
  })

  it('domain: closing a round is permanent and freezes its review data', async () => {
    let closed = false
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, candidateCount: 0 }),
            closed
              ? closedRound({ id: 2, roundNumber: 2, candidateCount: 1 })
              : openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
          ],
          progress: { processedCandidates: 0, totalCandidates: 1 },
        }),
      (url, init) => {
        if (init?.method === 'PUT' && url.endsWith('/rounds/2/close')) {
          closed = true
          return Promise.resolve(
            jsonResponse({
              id: 2,
              vacancyId: 1,
              roundNumber: 2,
              name: null,
              status: 'closed',
              closedAt: '2026-09-07T10:00:00Z',
              candidateCount: 1,
            }),
          )
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await wrapper.find('button[aria-label="Close Round 2"]').trigger('click')
    await flushPromises()

    // The confirm identifies the selected round and spells out that closing can’t be undone.
    expect(document.body.textContent).toContain('Round 2')
    expect(document.body.textContent).toContain('Closing is permanent')
    expect(document.body.textContent).toContain("can't be reopened")

    dialogButton('Close round')?.click()
    await flushPromises()

    expect(closed).toBe(true)
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Round 2 closed', color: 'success' }),
    )

    // No round is active now: the close affordance is gone, the frozen round's
    // candidates stay readable but read-only, and opening the next round is offered.
    expect(wrapper.find('button[aria-label="Close Round 2"]').exists()).toBe(false)
    expect(wrapper.findAll('button[aria-label="Delete candidate"]')).toHaveLength(0)
    expect(wrapper.find('[aria-label="Candidates"]').text()).toContain('Alice Applicant')
    expect(
      wrapper.findAll('button').some((button) => button.text().includes('New round')),
    ).toBe(true)
    wrapper.unmount()
  })

  it('domain: with no active round, HR opens the next round from the empty state', async () => {
    let opened = false
    let sentBody: unknown
    stubFetch(
      () =>
        vacancyDetails({
          rounds: opened
            ? [
                closedRound({ id: 1, roundNumber: 1, candidateCount: 0 }),
                openRound({ id: 2, roundNumber: 2, name: 'Second wave', candidateCount: 0 }),
              ]
            : [closedRound({ id: 1, roundNumber: 1, candidateCount: 0 })],
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/vacancies/1/rounds')) {
          opened = true
          sentBody = JSON.parse(String(init.body))
          return Promise.resolve(
            jsonResponse(
              {
                id: 2,
                vacancyId: 1,
                roundNumber: 2,
                name: 'Second wave',
                status: 'open',
                closedAt: null,
                candidateCount: 0,
              },
              201,
            ),
          )
        }
        if (url.includes('/rounds/1/candidates') || url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    // One closed round and none active: chrome stays quiet and imports wait.
    expect(wrapper.find('[aria-label="Intake rounds"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('No active round')
    expect(
      wrapper.findAll('button').filter((button) => button.text().includes('Import candidates')),
    ).toHaveLength(0)

    const openRoundButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Open a round'))
    expect(openRoundButton, 'an Open a round action in the empty state').toBeDefined()
    await openRoundButton!.trigger('click')
    await flushPromises()

    const nameInput = document.body.querySelector('input[aria-label="Round name"]')
    expect(nameInput, 'the round name input in the dialog').not.toBeNull()
    await new DOMWrapper(nameInput as HTMLInputElement).setValue('Second wave')
    dialogButton('Open round')?.click()
    await flushPromises()

    expect(sentBody).toEqual({ name: 'Second wave' })
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Round 2 — Second wave opened', color: 'success' }),
    )

    // The workspace moves into the new active round, ready for imports.
    expect(wrapper.find('[aria-label="Intake rounds"]').exists()).toBe(true)
    expect(wrapper.find('[aria-current="true"]').text()).toContain('Round 2')
    expect(wrapper.text()).toContain('No candidates yet')
    expect(
      wrapper.findAll('button').some((button) => button.text().includes('Import candidates')),
    ).toBe(true)
    wrapper.unmount()
  })

  it('domain: the server refuses to open a second active round', async () => {
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [closedRound({ id: 1, roundNumber: 1, candidateCount: 0 })],
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/vacancies/1/rounds')) {
          return Promise.resolve(
            jsonResponse(
              { title: 'Conflict', detail: 'Another intake round is already active.' },
              409,
            ),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    const openRoundButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Open a round'))
    await openRoundButton!.trigger('click')
    await flushPromises()
    const requestCountBeforeConflict = vi.mocked(fetch).mock.calls.length
    dialogButton('Open round')?.click()
    await flushPromises()

    // The 409 surfaces as a toast, the dialog stays open, and nothing changed.
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Couldn't save that change",
        description: "The data changed on the server. We've refreshed -- try again.",
        color: 'error',
      }),
    )
    expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThan(
      requestCountBeforeConflict,
    )
    expect(document.body.textContent).toContain('Open a new round')
    expect(wrapper.text()).toContain('No active round')
    wrapper.unmount()
  })

  it('domain: an active-round conflict warns and refreshes the round manager', async () => {
    let conflictObserved = false
    stubFetch(
      () =>
        vacancyDetails({
          rounds: [
            conflictObserved
              ? openRound({ id: 1, roundNumber: 1, candidateCount: 0 })
              : closedRound({ id: 1, roundNumber: 1, candidateCount: 0 }),
          ],
        }),
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/vacancies/1/rounds')) {
          conflictObserved = true
          return Promise.resolve(
            jsonResponse({ title: 'IntakeRounds.ActiveRoundExists' }, 409),
          )
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Open a round'))
      ?.trigger('click')
    await flushPromises()
    const requestCountBeforeConflict = vi.mocked(fetch).mock.calls.length
    dialogButton('Open round')?.click()
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'A round is already active',
        description: 'Close the active round before opening a new one.',
        color: 'warning',
      }),
    )
    expect(document.body.textContent).toContain('Open a new round')
    expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThan(requestCountBeforeConflict)
    wrapper.unmount()
  })

  it('domain: promotion from a round that is not closed keeps the dialog open with its name', async () => {
    let vacancyRequests = 0
    stubFetch(
      () => {
        vacancyRequests += 1
        return vacancyDetails({
          rounds: [
            closedRound({ id: 1, roundNumber: 1, name: 'First wave', candidateCount: 1 }),
            openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
          ],
        })
      },
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/rounds/2/promotions')) {
          return Promise.resolve(jsonResponse({ title: 'IntakeRounds.NotClosed' }, 409))
        }
        if (url.includes('/rounds/1/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        if (url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(2)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Promote from'))
      ?.trigger('click')
    await flushPromises()
    const candidateCheckbox = new DOMWrapper(
      document.body.querySelector('input[aria-label="Promote Alice Applicant"]') as HTMLInputElement,
    )
    await candidateCheckbox.setValue(true)
    await new DOMWrapper(
      Array.from(document.body.querySelectorAll('button')).find((button) =>
        button.textContent?.includes('Promote selected'),
      ) as HTMLButtonElement,
    ).trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain("Round 1 — First wave isn't closed yet")
    expect(document.body.textContent).toContain('You can only promote from closed rounds.')
    expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()
    expect(vacancyRequests).toBeGreaterThan(1)
    wrapper.unmount()
  })

  it('domain: promotion without an active round shows the lifecycle guidance and refreshes', async () => {
    let vacancyRequests = 0
    stubFetch(
      () => {
        vacancyRequests += 1
        return vacancyDetails({
          rounds:
            vacancyRequests > 1
              ? [closedRound({ id: 1, roundNumber: 1, name: 'First wave', candidateCount: 1 })]
              : [
                  closedRound({ id: 1, roundNumber: 1, name: 'First wave', candidateCount: 1 }),
                  openRound({ id: 2, roundNumber: 2, candidateCount: 1 }),
                ],
        })
      },
      (url, init) => {
        if (init?.method === 'POST' && url.endsWith('/rounds/2/promotions')) {
          return Promise.resolve(jsonResponse({ title: 'IntakeRounds.NoActiveRound' }, 409))
        }
        if (url.includes('/rounds/1/candidates') || url.includes('/rounds/2/candidates')) {
          return Promise.resolve(jsonResponse([candidateSummary(1)]))
        }
        return undefined
      },
    )

    const { wrapper } = mountView()
    await flushPromises()

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Promote from'))
      ?.trigger('click')
    await flushPromises()
    const candidateCheckbox = new DOMWrapper(
      document.body.querySelector('input[aria-label="Promote Alice Applicant"]') as HTMLInputElement,
    )
    await candidateCheckbox.setValue(true)
    await new DOMWrapper(
      Array.from(document.body.querySelectorAll('button')).find((button) =>
        button.textContent?.includes('Promote selected'),
      ) as HTMLButtonElement,
    ).trigger('click')
    await flushPromises()

    expect(document.body.textContent).toContain('No active round')
    expect(document.body.textContent).toContain(
      'Open a round first, then promote these candidates into it.',
    )
    expect(document.body.querySelector('[role="dialog"]')).not.toBeNull()
    expect(vacancyRequests).toBeGreaterThan(1)
    wrapper.unmount()
  })
})
