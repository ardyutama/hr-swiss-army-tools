import { effectScope, ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { CandidateSummary } from '@/features/candidates/api'
import type { RenderedMessage } from '@/features/email-templates/api'
import { usePreparedMessages } from './usePreparedMessages'

const emailTemplateMocks = vi.hoisted(() => ({
  listEmailTemplates: vi.fn(),
  renderEmailTemplate: vi.fn(),
}))

vi.mock('@/features/email-templates/api', async () => {
  const actual = await vi.importActual<typeof import('@/features/email-templates/api')>(
    '@/features/email-templates/api',
  )
  return { ...actual, ...emailTemplateMocks }
})

type Deferred<T> = {
  promise: Promise<T>
  resolve: (value: T) => void
}

function deferred<T>(): Deferred<T> {
  let resolve!: (value: T) => void
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

function candidate(): CandidateSummary {
  return {
    id: 7,
    fullName: 'Candidate 7',
    contactEmail: 'candidate-7@example.com',
    notes: null,
    reviewStatus: 'shortlisted',
    hireOutcome: 'none',
    isResubmitted: false,
    sourceSenderName: null,
    sourceSenderEmail: null,
    sourceSubject: null,
    sourceSentAt: null,
    cvDocumentCount: 0,
  }
}

const template = {
  id: 1,
  vacancyId: 1,
  kind: 'shortlisted' as const,
  subject: 'Hello {{candidate_name}}',
  body: 'A prepared message',
}

describe('US-19: prepared message loading', () => {
  let scope: ReturnType<typeof effectScope> | null = null

  beforeEach(() => {
    emailTemplateMocks.listEmailTemplates.mockReset()
    emailTemplateMocks.renderEmailTemplate.mockReset()
    emailTemplateMocks.listEmailTemplates.mockResolvedValue([template])
  })

  afterEach(() => {
    scope?.stop()
    scope = null
  })

  it('does not let an older load overwrite the newest rendered rows', async () => {
    const renders = [deferred<RenderedMessage>(), deferred<RenderedMessage>()]
    emailTemplateMocks.renderEmailTemplate.mockImplementation(
      () => renders[emailTemplateMocks.renderEmailTemplate.mock.calls.length - 1]!.promise,
    )
    const target = candidate()
    scope = effectScope()
    const prepared = scope.run(() =>
      usePreparedMessages(ref('1'), ref([target]), ref(false), ref(0)),
    )!

    const firstLoad = prepared.load()
    await flushPromises()
    expect(emailTemplateMocks.renderEmailTemplate).toHaveBeenCalledTimes(1)

    const secondLoad = prepared.load()
    await flushPromises()
    expect(emailTemplateMocks.renderEmailTemplate).toHaveBeenCalledTimes(2)

    const newestMessage = { subject: 'Generation 2', body: 'Newest content' }
    renders[1]!.resolve(newestMessage)
    await secondLoad
    expect(prepared.rows.value[0]?.state).toEqual({ kind: 'ready', message: newestMessage })

    renders[0]!.resolve({ subject: 'Generation 1', body: 'Stale content' })
    await firstLoad

    expect(prepared.rows.value[0]?.state).toEqual({ kind: 'ready', message: newestMessage })
  })
})