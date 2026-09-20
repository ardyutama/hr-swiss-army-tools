import { DOMWrapper, flushPromises, type VueWrapper } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import {
  bodyElement,
  bobSummary,
  candidateSummary,
  closedRound,
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

  async function selectMenuOption(trigger: Element, optionLabel: string) {
    await new DOMWrapper(trigger).trigger('keydown', { key: 'ArrowDown' })
    await flushPromises()

    const option = Array.from(document.body.querySelectorAll('[role="option"]')).find((candidate) =>
      candidate.textContent?.includes(optionLabel),
    )
    expect(option).toBeDefined()
    await new DOMWrapper(option as HTMLElement).trigger('keydown', { key: 'Enter' })
    await flushPromises()
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

  /** Stubs the Clipboard API for one test; afterEach removes the property again. */
  function stubClipboard() {
    const writeText = vi.fn<(text: string) => Promise<void>>().mockResolvedValue(undefined)
    Object.defineProperty(window.navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    })
    return writeText
  }

  describe('US-19: email templates', () => {
    function emailTemplate(kind: 'shortlisted' | 'rejected') {
      return {
        kind,
        subject: 'Good news, {{candidate_name}}',
        body: 'Hi {{candidate_name}}, we would like to invite you to an interview.',
      }
    }

    function plumberSource() {
      return {
        vacancyId: 9,
        vacancyTitle: 'Plumber',
        openedOn: '2026-01-15',
        subject: 'Copied subject',
        body: 'Copied body',
      }
    }

    function templateSection(heading: string): Element {
      return bodyElement(`[aria-label="${heading}"]`)
    }

    async function openEmailTemplatesDialog(wrapper: VueWrapper) {
      // The header button opens the prepared-messages dialog; the templates
      // dialog stacks on top of it through the dialog's Edit templates action.
      await openPreparedMessagesDialog(wrapper)
      await new DOMWrapper(buttonIn(topDialog(), 'Edit templates')).trigger('click')
      await flushPromises()
    }

    it('opens the email templates dialog with both template sections', async () => {
      stubFetch(() => vacancyDetails(), emailTemplatesFetch())

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      const dialog = topDialog()
      expect(dialog.textContent).toContain('Email templates')
      expect(dialog.textContent).toContain('Welder · Opened')
      expect(dialog.textContent).toContain('Shortlisted template')
      expect(dialog.textContent).toContain('Rejected template')
      expect(dialog.textContent).toContain('No shortlisted template yet.')
      expect(dialog.textContent).toContain('No rejected template yet.')
      wrapper.unmount()
    })

    it('creates the shortlisted template with the typed subject and body', async () => {
      let putBody: unknown
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          onUpsert: (_kind, body) => {
            putBody = body
            return { kind: 'shortlisted', ...(body as object) }
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      await new DOMWrapper(buttonIn(templateSection('Shortlisted template'), 'Create template')).trigger(
        'click',
      )
      await flushPromises()

      const editor = topDialog()
      expect(editor.textContent).toContain('Create shortlisted template')
      // The viewed round has no candidates: the draft passes through unrendered.
      expect(editor.textContent).toContain('Import candidates to preview a prepared message.')

      await new DOMWrapper(editor.querySelector('input') as HTMLElement).setValue(
        'Welcome to the team',
      )
      await new DOMWrapper(editor.querySelector('textarea') as HTMLElement).setValue(
        'Hi {{candidate_name}}, welcome!',
      )
      await new DOMWrapper(buttonIn(editor, 'Create template')).trigger('click')
      await flushPromises()

      expect(putBody).toEqual({
        subject: 'Welcome to the team',
        body: 'Hi {{candidate_name}}, welcome!',
      })
      expect(toastAdd).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Shortlisted template saved', color: 'success' }),
      )
      expect(templateSection('Shortlisted template').textContent).toContain('Welcome to the team')
      wrapper.unmount()
    })

    it('views a template and renders the prepared message for the matching candidate', async () => {
      let renderBody: unknown
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [emailTemplate('shortlisted')],
          candidates: () => [candidateSummary(1), bobSummary()],
          onRender: (body) => {
            renderBody = body
            return {
              subject: 'Good news, Bob Builder',
              body: 'Hi Bob Builder, we would like to invite you to an interview.',
            }
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      await new DOMWrapper(buttonIn(templateSection('Shortlisted template'), 'View')).trigger(
        'click',
      )
      await flushPromises()

      // The default preview candidate is the first one matching the template kind
      // (Bob is shortlisted; Alice is new).
      expect(renderBody).toEqual({
        subject: 'Good news, {{candidate_name}}',
        body: 'Hi {{candidate_name}}, we would like to invite you to an interview.',
        candidateId: 2,
      })
      const viewer = topDialog()
      expect(viewer.textContent).toContain('Prepared message')
      expect(viewer.textContent).toContain('Good news, Bob Builder')

      const previewPicker = viewer.querySelector('[aria-label="Preview for"]')
      expect(previewPicker).not.toBeNull()
      vi.useFakeTimers()
      try {
        await selectMenuOption(previewPicker as HTMLElement, 'Alice Applicant')
        await vi.advanceTimersByTimeAsync(350)
        await flushPromises()
      } finally {
        vi.useRealTimers()
      }
      expect(renderBody).toMatchObject({ candidateId: 1 })
      wrapper.unmount()
    })

    it('copies a template from a previous vacancy into a pre-filled editor', async () => {
      let putBody: unknown
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          sources: { shortlisted: [plumberSource()] },
          onUpsert: (_kind, body) => {
            putBody = body
            return { kind: 'shortlisted', ...(body as object) }
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      const section = templateSection('Shortlisted template')
      expect(section.textContent).toContain('Copy from a previous vacancy')
      const copyPicker = section.querySelector('[aria-label="Copy from a previous vacancy"]')
      expect(copyPicker).not.toBeNull()
      await selectMenuOption(copyPicker as HTMLElement, 'Plumber')

      const editor = topDialog()
      expect(editor.textContent).toContain('Copied from Plumber')
      expect((editor.querySelector('input') as HTMLInputElement).value).toBe('Copied subject')
      expect((editor.querySelector('textarea') as HTMLTextAreaElement).value).toBe('Copied body')

      // Nothing persisted until HR saves; the save upserts an independent copy.
      await new DOMWrapper(buttonIn(editor, 'Create template')).trigger('click')
      await flushPromises()

      expect(putBody).toEqual({ subject: 'Copied subject', body: 'Copied body' })
      wrapper.unmount()
    })

    it('keeps a copied template after closing and reopening email templates', async () => {
      let storedTemplates: unknown[] = []
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => storedTemplates,
          sources: { shortlisted: [plumberSource()] },
          onUpsert: (_kind, body) => {
            const template = { id: 1, vacancyId: 1, kind: 'shortlisted', ...(body as object) }
            storedTemplates = [template]
            return template
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      const section = templateSection('Shortlisted template')
      const copyPicker = section.querySelector('[aria-label="Copy from a previous vacancy"]')
      expect(copyPicker).not.toBeNull()
      await selectMenuOption(copyPicker as HTMLElement, 'Plumber')

      const editor = topDialog()
      await new DOMWrapper(buttonIn(editor, 'Create template')).trigger('click')
      await flushPromises()

      // The templates dialog is stacked on the prepared-messages dialog; close the top one.
      const closeButton = topDialog().querySelector('button[aria-label="Close"]')
      expect(closeButton).not.toBeNull()
      await new DOMWrapper(closeButton as HTMLButtonElement).trigger('click')
      await flushPromises()

      await openEmailTemplatesDialog(wrapper)
      expect(templateSection('Shortlisted template').textContent).toContain('Copied subject')
      wrapper.unmount()
    })

    it('deletes a template through the inline confirm', async () => {
      let deletedKind: string | undefined
      let templateGone = false
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () =>
            templateGone
              ? []
              : [emailTemplate('shortlisted')],
          onDelete: (kind) => {
            deletedKind = kind
            templateGone = true
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      const section = templateSection('Shortlisted template')
      await new DOMWrapper(buttonIn(section, 'Delete')).trigger('click')
      await flushPromises()

      // The section footer morphs into the inline confirm.
      expect(section.textContent).toContain('Delete this template?')
      await new DOMWrapper(buttonIn(section, 'Delete')).trigger('click')
      await flushPromises()

      expect(deletedKind).toBe('shortlisted')
      expect(toastAdd).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Shortlisted template deleted', color: 'success' }),
      )
      expect(templateSection('Shortlisted template').textContent).toContain(
        'No shortlisted template yet.',
      )
      wrapper.unmount()
    })

    it('opens read-only for a closed vacancy: mutations hidden, View and copy-from remain', async () => {
      let putCalls = 0
      stubFetch(
        () =>
          vacancyDetails({
            status: 'closed',
            closedAt: '2026-09-10T00:00:00Z',
            rounds: [closedRound({ candidateCount: 1 })],
          }),
        emailTemplatesFetch({
          templates: () => [emailTemplate('shortlisted')],
          sources: { rejected: [plumberSource()] },
          candidates: () => [bobSummary()],
          onUpsert: (_kind, body) => {
            putCalls += 1
            return body
          },
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openEmailTemplatesDialog(wrapper)

      const dialog = topDialog()
      expect(dialog.textContent).toContain(
        'Vacancy closed — templates are settled. Reopen the vacancy to change them.',
      )

      const shortlisted = templateSection('Shortlisted template')
      expect(buttonIn(shortlisted, 'View')).toBeDefined()
      expect(
        shortlisted.querySelectorAll('button').length,
        'no mutating buttons on the existing template',
      ).toBe(1)

      const rejected = templateSection('Rejected template')
      expect(rejected.textContent).toContain('No rejected template yet.')
      expect(rejected.textContent).not.toContain('Create template')
      // Copy-from stays available on a closed vacancy.
      const copyPicker = rejected.querySelector('[aria-label="Copy from a previous vacancy"]')
      expect(copyPicker).not.toBeNull()
      await selectMenuOption(copyPicker as HTMLElement, 'Plumber')

      const copiedSource = topDialog()
      expect(copiedSource.textContent).toContain('Template copied from Plumber')
      expect(copiedSource.textContent).not.toContain('Create template')
      const copiedSubject = copiedSource.querySelector('input') as HTMLInputElement
      const copiedBody = copiedSource.querySelector('textarea') as HTMLTextAreaElement
      expect(copiedSubject.value).toBe('Copied subject')
      expect(copiedBody.value).toBe('Copied body')
      expect(copiedSubject.readOnly).toBe(true)
      expect(copiedBody.readOnly).toBe(true)
      expect(putCalls).toBe(0)
      wrapper.unmount()
    })
  })

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

    function rejectedTemplate() {
      return {
        id: 2,
        vacancyId: 1,
        kind: 'rejected',
        subject: 'Thank you, {{candidate_name}}',
        body: 'Hi {{candidate_name}}, thank you for applying.',
      }
    }

    it('US-19: HR opens one candidate’s prepared message and sends it from their mail client', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate()],
          candidates: () => [bobSummary({ contactEmail: 'bob@example.com' })],
          onRender: () => ({
            subject: 'Good news, Bob Builder',
            body: 'Hi Bob Builder,\n\nWe would like to invite you to an interview.',
          }),
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()

      const bobRow = wrapper.findAll('.crow').find((row) => row.text().includes('Bob Builder'))
      await bobRow!.find('button[aria-label="Send email"]').trigger('click')
      await flushPromises()

      // The row action opens the same dialog as Send To All, filtered to Bob.
      const dialog = topDialog()
      expect(dialog.textContent).toContain('Prepared messages')
      expect(dialog.textContent).toContain('1 candidate in Round 1')
      expect(dialog.textContent).toContain('Bob Builder')
      expect(dialog.textContent).toContain('Good news, Bob Builder')
      expect(dialog.textContent).toContain('We would like to invite you to an interview.')

      // RFC 6068: header values percent-encoded, body line breaks as CRLF.
      const mailto = dialog.querySelector('a[href^="mailto:"]') as HTMLAnchorElement
      expect(mailto).not.toBeNull()
      expect(mailto.getAttribute('href')).toBe(
        'mailto:bob@example.com?subject=Good%20news%2C%20Bob%20Builder&body=Hi%20Bob%20Builder%2C%0D%0A%0D%0AWe%20would%20like%20to%20invite%20you%20to%20an%20interview.',
      )
      wrapper.unmount()
    })

    it('US-19: Send To All prepares one message per contactable candidate and names who was left out', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate(), rejectedTemplate()],
          candidates: () => [
            candidateSummary(1),
            bobSummary({ contactEmail: 'bob@example.com' }),
            candidateSummary(3, {
              sourceSenderName: 'Carol Welder',
              sourceSenderEmail: 'carol@example.com',
              reviewStatus: 'rejected',
              contactEmail: 'carol@example.com',
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
          ],
          onRender: (body) => {
            const { candidateId } = body as { candidateId: number }
            const names: Record<number, string> = { 2: 'Bob Builder', 3: 'Carol Welder' }
            return {
              subject: `Update for ${names[candidateId]}`,
              body: `Dear ${names[candidateId]}`,
            }
          },
        }),
      )

      const { router, wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      const dialog = topDialog()
      // Scope is the selected round's full list; exclusions are counted with reasons.
      expect(dialog.textContent).toContain('5 candidates in Round 1')
      expect(dialog.textContent).toContain(
        'Contacting 2 of 5 candidates — 1 is new or flagged, 1 already has a hire outcome.',
      )

      // One prepared message per Contactable Candidate, each rendered with their data.
      expect(dialog.querySelectorAll('a[href^="mailto:"]')).toHaveLength(2)
      expect(dialog.textContent).toContain('Update for Bob Builder')
      expect(dialog.textContent).toContain('Update for Carol Welder')
      expect(dialog.textContent).not.toContain('Hilda')
      expect(dialog.textContent).not.toContain('Alice Applicant')

      // The email-less rejected candidate is named in her own section, never silently skipped.
      const missingSection = dialog.querySelector('[aria-label="Needs an email address"]')
      expect(missingSection).not.toBeNull()
      expect(missingSection!.textContent).toContain('Eve Emailless')

      // The fix path jumps to her review workspace and closes the dialog.
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

    it('US-19: a missing template blocks that kind with a path to create it', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [rejectedTemplate()],
          candidates: () => [
            bobSummary({ contactEmail: 'bob@example.com' }),
            candidateSummary(3, {
              sourceSenderName: 'Carol Welder',
              sourceSenderEmail: 'carol@example.com',
              reviewStatus: 'rejected',
              contactEmail: 'carol@example.com',
            }),
          ],
          onRender: () => ({ subject: 'Update for Carol Welder', body: 'Dear Carol Welder' }),
        }),
      )

      const { wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      const dialog = topDialog()
      // The rejected candidate's message is prepared; the missing shortlisted kind
      // is called out with a path forward — the app never invents default copy.
      expect(dialog.textContent).toContain('Update for Carol Welder')
      expect(dialog.textContent).toContain('No shortlisted template yet')
      expect(dialog.textContent).not.toContain('bob@example.com')

      // Create template stacks the templates dialog on top of the prepared list.
      await new DOMWrapper(buttonIn(dialog, 'Create template')).trigger('click')
      await flushPromises()
      expect(topDialog().textContent).toContain('Email templates')
      wrapper.unmount()
    })

    it('US-19: Copy writes the prepared message to the clipboard as To/Subject/body', async () => {
      stubFetch(
        () => vacancyDetails(),
        emailTemplatesFetch({
          templates: () => [shortlistedTemplate(), rejectedTemplate()],
          candidates: () => [
            bobSummary({ contactEmail: 'bob@example.com' }),
            candidateSummary(3, {
              sourceSenderName: 'Carol Welder',
              sourceSenderEmail: 'carol@example.com',
              reviewStatus: 'rejected',
              contactEmail: 'carol@example.com',
            }),
          ],
          onRender: (body) => {
            const { candidateId } = body as { candidateId: number }
            const names: Record<number, string> = { 2: 'Bob Builder', 3: 'Carol Welder' }
            return {
              subject: `Update for ${names[candidateId]}`,
              body: `Dear ${names[candidateId]},\n\nSee you soon.`,
            }
          },
        }),
      )
      const writeText = stubClipboard()

      const { wrapper } = mountView()
      await flushPromises()
      await openPreparedMessagesDialog(wrapper)

      const dialog = topDialog()
      const carolRow = Array.from(dialog.querySelectorAll('li')).find((item) =>
        item.textContent?.includes('Carol Welder'),
      )
      expect(carolRow, "Carol's prepared message row").toBeDefined()
      await new DOMWrapper(buttonIn(carolRow as Element, 'Copy')).trigger('click')
      await flushPromises()

      // The clipboard fallback carries headers, a blank line, then the body; the row confirms.
      expect(writeText).toHaveBeenCalledWith(
        'To: carol@example.com\nSubject: Update for Carol Welder\n\nDear Carol Welder,\n\nSee you soon.',
      )
      expect(buttonIn(carolRow as Element, 'Copied')).toBeDefined()
      wrapper.unmount()
    })
  })
})
