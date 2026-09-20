import { DOMWrapper, flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, shallowRef, toRef, type PropType } from 'vue'

import { candidateDisplayName } from '@/features/candidates/format'
import type {
  EmailTemplateKind,
  EmailTemplateWritePayload,
} from '@/features/email-templates/api'
import EmailTemplatesDialog from '@/features/email-templates/components/EmailTemplatesDialog.vue'
import PreparedMessageListDialog from '@/features/prepared-messages/components/PreparedMessageListDialog.vue'
import type { VacancyRound } from '@/features/vacancies/api'
import { useMessagingConsole } from './useMessagingConsole'

// The messaging console has no production wrapper component: the page binds it.
// This spec-local harness (the testing.md spec-local harness admission, the
// import-console precedent) wires the composer to the flow's two dialogs and
// its entry chrome so the messaging scenarios run at the console's seam. It is
// scaffolding local to this spec — never production code.
const MessagingConsoleHarness = defineComponent({
  props: {
    id: { type: String, required: true },
    // Feeds the templates dialog's read-only mode; the closed-vacancy scenario
    // mounts with 'closed'.
    status: { type: String as PropType<'open' | 'closed'>, default: 'open' },
  },
  setup(props) {
    const vacancyId = toRef(props, 'id')
    const roundId = shallowRef('1')
    // The console's one send-scope input living outside the leaves: the
    // selected round, read only for the send dialog's round naming.
    const selectedRound = shallowRef<VacancyRound | null>({
      id: 1,
      roundNumber: 1,
      name: null,
      status: 'open',
      closedAt: null,
      candidateCount: 0,
    })
    const messaging = useMessagingConsole({ vacancyId, roundId, selectedRound })
    const { preparedMessages, emailTemplates } = messaging
    const summaries = messaging.messagingSummary.summaries

    // The dialog emits save/remove; the harness owns the try/catch and settles
    // the outcome back through the exposed handles — the same canonical
    // form-dialog seam the view runs.
    const templatesDialog = shallowRef<InstanceType<typeof EmailTemplatesDialog>>()

    async function onSave(kind: EmailTemplateKind, payload: EmailTemplateWritePayload) {
      try {
        await emailTemplates.save(kind, payload)
        templatesDialog.value?.saveSucceeded()
      } catch (error) {
        templatesDialog.value?.saveFailed(error)
      }
    }

    async function onRemove(kind: EmailTemplateKind) {
      try {
        await emailTemplates.remove(kind)
        templatesDialog.value?.removeSettled(kind)
      } catch (error) {
        templatesDialog.value?.removeSettled(kind, error)
      }
    }

    return () =>
      h('div', [
        h(
          'button',
          { type: 'button', onClick: () => messaging.openPrepared() },
          'Send email to all candidates',
        ),
        // Per-candidate send mirrors the page's CandidateList row action.
        ...(summaries.value ?? []).map((candidate) =>
          h(
            'button',
            {
              type: 'button',
              key: candidate.id,
              onClick: () => messaging.openPrepared(candidate),
            },
            `Send email to ${candidateDisplayName(candidate)}`,
          ),
        ),
        h(PreparedMessageListDialog, {
          open: messaging.preparedOpen.value,
          'onUpdate:open': (open: boolean) => {
            messaging.preparedOpen.value = open
          },
          candidateCount: messaging.sendCandidates.value.length,
          roundName: messaging.sendRoundName.value,
          viewState: preparedMessages.viewState.value,
          loadError: preparedMessages.loadError.value,
          rows: preparedMessages.rows.value,
          contactableCount: preparedMessages.contactableCount.value,
          missingEmail: preparedMessages.missingEmail.value,
          excludedUndecided: preparedMessages.excludedUndecided.value,
          excludedOutcome: preparedMessages.excludedOutcome.value,
          missingTemplateKinds: preparedMessages.missingTemplateKinds.value,
          copiedCandidateId: preparedMessages.copiedCandidateId.value,
          onEditTemplates: messaging.openEmailTemplates,
          // The router navigation this emit drives stays page-level (the
          // composer exposes nothing for it) — the harness has no router.
          onOpenReview: () => {},
          onRetry: preparedMessages.retry,
          onCopy: preparedMessages.copy,
          onReload: preparedMessages.load,
        }),
        h(EmailTemplatesDialog, {
          ref: templatesDialog,
          open: messaging.emailTemplatesOpen.value,
          'onUpdate:open': (open: boolean) => {
            messaging.emailTemplatesOpen.value = open
          },
          vacancyId: props.id,
          vacancyTitle: 'Welder',
          openedOn: '2026-08-27',
          status: props.status,
          candidates: summaries.value ?? [],
          templates: emailTemplates.templates.value,
          loadError: emailTemplates.loadError.value,
          viewState: emailTemplates.viewState.value,
          saving: emailTemplates.saving.value,
          deleting: emailTemplates.deleting.value,
          sources: emailTemplates.sources.value,
          onReload: emailTemplates.load,
          onLoadSources: emailTemplates.loadSources,
          onSave,
          onRemove,
        }),
      ])
  },
})

const toastAdd = vi.hoisted(() => vi.fn())

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
}))

function mountConsole(props: { status?: 'open' | 'closed' } = {}) {
  return mount(MessagingConsoleHarness, { props: { id: '1', ...props } })
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

type FetchHandler = (url: string, init?: RequestInit) => Promise<Response> | undefined

// The console touches five endpoints: the messaging summary (eager on mount)
// and the four email-templates routes behind the dialogs. Anything else is
// unstubbed and fails the test.
function stubFetch(handler: FetchHandler): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      const matched = handler(url, init)
      if (matched !== undefined) {
        return matched
      }
      throw new Error(`Unstubbed fetch: ${init?.method ?? 'GET'} ${url}`)
    }),
  )
}

/** Serves the messaging summary plus the email-templates endpoints; anything else still throws via stubFetch. */
function messagingFetch(
  options: {
    templates?: () => unknown
    sources?: Record<string, unknown[]>
    candidates?: () => unknown[]
    onUpsert?: (kind: string, body: unknown) => unknown
    onDelete?: (kind: string) => void
    onRender?: (body: unknown) => unknown
  } = {},
): FetchHandler {
  return (url, init) => {
    if (url.endsWith('/vacancies/1/email-templates')) {
      return Promise.resolve(jsonResponse(options.templates?.() ?? []))
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
    // Eager on mount: the round's full unpaged list feeds both dialogs.
    if (url.endsWith('/messaging-summary')) {
      return Promise.resolve(jsonResponse(options.candidates?.() ?? []))
    }
    return undefined
  }
}

function candidateSummary(id: number, overrides: Partial<Record<string, unknown>> = {}) {
  return {
    id,
    fullName: null,
    contactEmail: null,
    notes: null,
    reviewStatus: 'new',
    hireOutcome: 'none',
    isResubmitted: false,
    intakeSource: 'email',
    screenedOut: false,
    firedRules: [] as { index: number; display: string }[],
    sourceSenderName: 'Alice Applicant',
    sourceSenderEmail: 'alice@example.com',
    sourceSubject: 'Application for Welder',
    sourceSentAt: '2026-08-28T09:00:00Z',
    cvDocumentCount: 1,
    ...overrides,
  }
}

function bobSummary(overrides: Partial<Record<string, unknown>> = {}) {
  return candidateSummary(2, {
    sourceSenderName: 'Bob Builder',
    sourceSenderEmail: 'bob@example.com',
    sourceSubject: 'Bob application',
    reviewStatus: 'shortlisted',
    ...overrides,
  })
}

function bodyElement(selector: string): Element {
  const element = document.body.querySelector(selector)
  if (!element) {
    throw new Error(`Expected "${selector}" to exist in document.body`)
  }
  return element
}

// The dialogs teleport to document.body.
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

/** Opens the prepared-messages dialog through the Send To All chrome button. */
async function openPreparedDialog(wrapper: VueWrapper) {
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

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
  delete (window.navigator as { clipboard?: unknown }).clipboard
})

describe('useMessagingConsole · email templates and prepared messages', () => {
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
      // The Send To All button opens the prepared-messages dialog; the
      // templates dialog stacks on top of it through the Edit templates action.
      await openPreparedDialog(wrapper)
      await new DOMWrapper(buttonIn(topDialog(), 'Edit templates')).trigger('click')
      await flushPromises()
    }

    it('opens the email templates dialog with both template sections', async () => {
      stubFetch(messagingFetch())

      const wrapper = mountConsole()
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
        messagingFetch({
          onUpsert: (_kind, body) => {
            putBody = body
            return { kind: 'shortlisted', ...(body as object) }
          },
        }),
      )

      const wrapper = mountConsole()
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
        messagingFetch({
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

      const wrapper = mountConsole()
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
        messagingFetch({
          sources: { shortlisted: [plumberSource()] },
          onUpsert: (_kind, body) => {
            putBody = body
            return { kind: 'shortlisted', ...(body as object) }
          },
        }),
      )

      const wrapper = mountConsole()
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
        messagingFetch({
          templates: () => storedTemplates,
          sources: { shortlisted: [plumberSource()] },
          onUpsert: (_kind, body) => {
            const template = { id: 1, vacancyId: 1, kind: 'shortlisted', ...(body as object) }
            storedTemplates = [template]
            return template
          },
        }),
      )

      const wrapper = mountConsole()
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
        messagingFetch({
          templates: () => (templateGone ? [] : [emailTemplate('shortlisted')]),
          onDelete: (kind) => {
            deletedKind = kind
            templateGone = true
          },
        }),
      )

      const wrapper = mountConsole()
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
        messagingFetch({
          templates: () => [emailTemplate('shortlisted')],
          sources: { rejected: [plumberSource()] },
          candidates: () => [bobSummary()],
          onUpsert: (_kind, body) => {
            putCalls += 1
            return body
          },
        }),
      )

      const wrapper = mountConsole({ status: 'closed' })
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
        messagingFetch({
          templates: () => [shortlistedTemplate()],
          candidates: () => [bobSummary({ contactEmail: 'bob@example.com' })],
          onRender: () => ({
            subject: 'Good news, Bob Builder',
            body: 'Hi Bob Builder,\n\nWe would like to invite you to an interview.',
          }),
        }),
      )

      const wrapper = mountConsole()
      await flushPromises()

      // The per-candidate chrome mirrors the page's row send: the same dialog,
      // filtered to Bob.
      const sendBob = wrapper
        .findAll('button')
        .find((candidate) => candidate.text().includes('Send email to Bob Builder'))
      expect(sendBob, 'a per-candidate send button').toBeDefined()
      await sendBob!.trigger('click')
      await flushPromises()

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
        messagingFetch({
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

      const wrapper = mountConsole()
      await flushPromises()
      await openPreparedDialog(wrapper)

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

      // The email-less rejected candidate is named in her own section, never silently
      // skipped. The fix path her button takes (the review workspace) is page wiring.
      const missingSection = dialog.querySelector('[aria-label="Needs an email address"]')
      expect(missingSection).not.toBeNull()
      expect(missingSection!.textContent).toContain('Eve Emailless')
      expect(buttonIn(missingSection as Element, 'Add email in review')).toBeDefined()
      wrapper.unmount()
    })

    it('US-19: a missing template blocks that kind with a path to create it', async () => {
      stubFetch(
        messagingFetch({
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

      const wrapper = mountConsole()
      await flushPromises()
      await openPreparedDialog(wrapper)

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
        messagingFetch({
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

      const wrapper = mountConsole()
      await flushPromises()
      await openPreparedDialog(wrapper)

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
