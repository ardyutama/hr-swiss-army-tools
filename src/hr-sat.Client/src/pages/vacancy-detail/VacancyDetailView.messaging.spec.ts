import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bobSummary,
  candidateSummary,
  jsonResponse,
  mountView,
  pagedCandidates,
  stubFetch,
  toastAdd,
  vacancyDetails,
  type CandidateListItem,
  type FetchHandler,
} from './mountVacancyDetail'

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

// The messaging scenarios live at the console seam —
// features/vacancy-detail/messagingConsole.spec.ts. This spec keeps only page
// wiring: the CandidateList send affordance, the entries into the two dialogs,
// and the router navigation out of the prepared list (ticket:
// messaging-console-seam, grill decision 7).
describe('VacancyDetailView · messaging (US-19)', () => {
  /** Serves the candidates list plus the email-templates endpoints; anything else still throws via stubFetch. */
  function emailTemplatesFetch(
    options: {
      templates?: () => unknown
      sources?: Record<string, unknown[]>
      candidates?: () => CandidateListItem[]
      onUpsert?: (kind: string, body: unknown) => unknown
      onDelete?: (kind: string) => void
      onRender?: (body: unknown) => unknown
    } = {},
  ): FetchHandler {
    return (url, init) => {
      if (url.endsWith('/vacancies/1/email-templates')) {
        return Promise.resolve(
          jsonResponse(options.templates?.() ?? []),
        )
      }
      if (init?.method === 'PUT' && url.includes('/email-templates/')) {
        const kind = url.slice(url.lastIndexOf('/') + 1)
        const body = JSON.parse(String(init.body))
        return Promise.resolve(jsonResponse(options.onUpsert?.(kind, body) ?? { kind, ...body }))
      }
      if (init?.method === 'DELETE' && url.includes('/email-templates/')) {
        options.onDelete?.(url.slice(url.lastIndexOf('/') + 1))
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      if (url.includes('/email-templates/sources')) {
        const kind = new URL(url, 'http://localhost').searchParams.get('kind') ?? ''
        return Promise.resolve(jsonResponse(options.sources?.[kind] ?? []))
      }
      if (init?.method === 'POST' && url.endsWith('/email-templates/render')) {
        const body = JSON.parse(String(init.body))
        return Promise.resolve(
          jsonResponse(
            options.onRender?.(body) ?? {
              subject: 'Good news, Bob Builder',
              body: 'Hi Bob Builder, we would like to invite you to an interview.',
            },
          ),
        )
      }
      // Row send buttons read the paged list; the send dialogs read the
      // unpaged messaging summary — the same fixture feeds both.
      if (url.includes('/rounds/1/candidates')) {
        return Promise.resolve(jsonResponse(pagedCandidates(options.candidates?.() ?? [])))
      }
      if (url.endsWith('/rounds/1/messaging-summary')) {
        return Promise.resolve(jsonResponse(options.candidates?.() ?? []))
      }
      return undefined
    }
  }

  function topDialog(): Element {
    const dialogs = document.body.querySelectorAll('[role="dialog"]')
    const top = dialogs[dialogs.length - 1]
    if (!top) {
      throw new Error('Expected an open dialog in document.body')
    }
    return top
  }

  function buttonIn(root: Element, text: string): HTMLButtonElement {
    const button = Array.from(root.querySelectorAll('button')).find((candidate) =>
      candidate.textContent?.includes(text),
    )
    if (!button) {
      throw new Error(`Expected a "${text}" button`)
    }
    return button as HTMLButtonElement
  }

  /** Opens the prepared-messages dialog through the header's Send To All button. */
  async function openPreparedMessagesDialog(wrapper: VueWrapper) {
    const button = wrapper
      .findAll('button')
      .find((candidate) => candidate.text().includes('Send email to all candidates'))
    expect(button, 'a Send email to all candidates button').toBeDefined()
    await button!.trigger('click')
    await flushPromises()
  }

  describe('US-19: send via mailto', () => {
    it('US-19: send stays disabled with the reason until a candidate is contactable', async () => {
      stubFetch(
        () => vacancyDetails(),
        (url) => {
          if (url.includes('/rounds/1/candidates')) {
            return Promise.resolve(
              jsonResponse(
                pagedCandidates([
                  candidateSummary(1),
                  bobSummary({ contactEmail: 'bob@example.com' }),
                  candidateSummary(3, {
                    sourceSenderName: 'Fred Flagged',
                    sourceSenderEmail: 'fred@example.com',
                    reviewStatus: 'flagged',
                  }),
                  candidateSummary(4, {
                    sourceSenderName: 'Hilda Hired',
                    sourceSenderEmail: 'hilda@example.com',
                    reviewStatus: 'shortlisted',
                    hireOutcome: 'hired',
                    contactEmail: 'hilda@example.com',
                  }),
                  candidateSummary(5, {
                    sourceSenderName: 'Eve Emailless',
                    sourceSenderEmail: 'eve@example.com',
                    reviewStatus: 'rejected',
                  }),
                ]),
              ),
            )
          }
          return undefined
        },
      )

      const { wrapper } = mountView()
      await flushPromises()

      const sendButtonIn = (name: string) =>
        wrapper
          .findAll('.crow')
          .find((row) => row.text().includes(name))!
          .find('button[aria-label*="Send email"]')

      // The only Contactable Candidate: shortlisted on the Bench with an email.
      const bobSend = sendButtonIn('Bob Builder')
      expect(bobSend.attributes('aria-label')).toBe('Send email')
      expect((bobSend.element as HTMLButtonElement).disabled).toBe(false)

      // Everyone else keeps a rendered but disabled button whose label says why.
      const blocked: Array<[string, string]> = [
        ['Alice Applicant', 'Send email — Not yet shortlisted or rejected'],
        ['Fred Flagged', 'Send email — Not yet shortlisted or rejected'],
        ['Hilda Hired', 'Send email — Hire outcome already recorded'],
        ['Eve Emailless', 'Send email — Needs an email address'],
      ]
      for (const [name, label] of blocked) {
        const button = sendButtonIn(name)
        expect(button.attributes('aria-label'), name).toBe(label)
        expect(button.attributes('title'), name).toBe(label)
        expect((button.element as HTMLButtonElement).disabled, name).toBe(true)
      }
      wrapper.unmount()
    })

    function shortlistedTemplate() {
      return {
        id: 1,
        vacancyId: 1,
        kind: 'shortlisted',
        subject: 'Good news, {{candidate_name}}',
        body: 'Hi {{candidate_name}}, we would like to invite you to an interview.',
      }
    }

    it('US-19: the row send action opens the prepared dialog scoped to that candidate', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate()],
          candidates: () => [bobSummary({ contactEmail: 'bob@example.com' })],
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()

      const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
      await bobRow!.find('button[aria-label="Send email"]').trigger('click')
      await flushPromises()

      // Scope wiring: the dialog serves that one candidate. Its content is
      // console-seam behavior, covered by messagingConsole.spec.ts.
      const dialog = topDialog()
      expect(dialog.textContent).toContain('Prepared messages')
      expect(dialog.textContent).toContain('1 candidate in Round 1')
      expect(dialog.textContent).toContain('Bob Builder')
      wrapper.unmount()
    })

    it('US-19: Send To All opens the prepared dialog scoped to the round', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate()],
          candidates: () => [candidateSummary(1), bobSummary({ contactEmail: 'bob@example.com' })],
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      // Scope wiring: the round's full list, not one candidate.
      const dialog = topDialog()
      expect(dialog.textContent).toContain('Prepared messages')
      expect(dialog.textContent).toContain('2 candidates in Round 1')
      wrapper.unmount()
    })

    it('US-19: Edit templates opens the email templates dialog', async () => {
      stubFetch(() => vacancyDetails(), emailTemplatesFetch())

      const { wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      await new DOMWrapper(buttonIn(topDialog(), 'Edit templates')).trigger('click')
      await flushPromises()

      expect(topDialog().textContent).toContain('Email templates')
      wrapper.unmount()
    })

    it('US-19: Add email in review navigates to the review workspace and closes the dialog', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate()],
          candidates: () => [
            bobSummary({ contactEmail: 'bob@example.com' }),
            candidateSummary(5, {
              sourceSenderName: 'Eve Emailless',
              sourceSenderEmail: 'eve@example.com',
              reviewStatus: 'rejected',
            }),
          ],
        }),
      )

      const { router, wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      // The fix path for an email-less candidate jumps to her review workspace.
      const missingSection = topDialog().querySelector('[aria-label="Needs an email address"]')
      expect(missingSection).not.toBeNull()
      await new DOMWrapper(buttonIn(missingSection as Element, 'Add email in review')).trigger(
        'click',
      )
      await flushPromises()
      expect(router.currentRoute.value.name).toBe('candidate-review')
      expect(router.currentRoute.value.params).toMatchObject({
        id: '1',
        roundId: '1',
        candidateId: '5',
      })
      await vi.waitFor(() => {
        expect(document.body.querySelector('[role="dialog"]')).toBeNull()
      })
      wrapper.unmount()
    })
  })
})
