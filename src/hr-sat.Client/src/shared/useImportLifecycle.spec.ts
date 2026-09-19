import { effectScope, shallowRef } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from './http'
import { problemMessageText } from './problem-details'
import { useImportLifecycle } from './useImportLifecycle'

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

describe('useImportLifecycle', () => {
  let scope: ReturnType<typeof effectScope> | null = null

  afterEach(() => {
    scope?.stop()
    scope = null
  })

  function setup(overrides: { onChanged?: (change: 'candidates') => Promise<void> } = {}) {
    scope = effectScope()
    return scope.run(() => {
      const vacancyId = shallowRef('1')
      const roundId = shallowRef('2')
      const lifecycle = useImportLifecycle<'candidates', string>({
        vacancyId,
        roundId,
        fallback: 'Something went wrong',
        mapMessage: problemMessageText,
        mapFieldErrors: (errors) => Object.values(errors).flat()[0] ?? 'Something went wrong',
        onChanged: overrides.onChanged,
      })
      return { vacancyId, roundId, lifecycle }
    })!
  }

  it('announces a success and reports the change after the announcement', async () => {
    const order: string[] = []
    const onChanged = vi.fn(async () => {
      order.push('changed')
    })
    const { lifecycle } = setup({ onChanged })

    const result = await lifecycle.attempt({
      change: 'candidates',
      operation: async () => {
        order.push('operation')
        return 42
      },
      announce: () => {
        order.push('announce')
        expect(lifecycle.busy.value).toBe(true)
      },
    })

    expect(result).toBe(42)
    expect(order).toEqual(['operation', 'announce', 'changed'])
    expect(onChanged).toHaveBeenCalledWith('candidates')
    expect(lifecycle.busy.value).toBe(false)
    expect(lifecycle.alert.value).toBeNull()
  })

  it('guards against re-entry while an attempt is in flight', async () => {
    const { lifecycle } = setup()
    const gate = deferred<string>()
    const operation = vi.fn(() => gate.promise)

    const first = lifecycle.attempt({ change: 'candidates', operation, announce: () => {} })
    const second = await lifecycle.attempt({
      change: 'candidates',
      operation,
      announce: () => {},
    })

    expect(second).toBeNull()
    expect(operation).toHaveBeenCalledTimes(1)
    gate.resolve('done')
    expect(await first).toBe('done')
  })

  it('maps field-error payloads verbatim and announces no change', async () => {
    const onChanged = vi.fn()
    const { lifecycle } = setup({ onChanged })

    const result = await lifecycle.attempt({
      change: 'candidates',
      operation: () =>
        Promise.reject(
          new ApiError(400, { errors: { files: ['At least one .eml file is required.'] } }),
        ),
      announce: () => {},
    })

    expect(result).toBeNull()
    expect(lifecycle.alert.value).toBe('At least one .eml file is required.')
    expect(onChanged).not.toHaveBeenCalled()
    expect(lifecycle.busy.value).toBe(false)
  })

  it('maps taxonomy messages through the caller mapper and refreshes on non-failure', async () => {
    const onChanged = vi.fn(async () => {})
    const { lifecycle } = setup({ onChanged })

    const result = await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(409, { title: 'IntakeRounds.Closed' })),
      announce: () => {},
    })

    expect(result).toBeNull()
    expect(lifecycle.alert.value).toContain('This round is closed')
    expect(onChanged).toHaveBeenCalledWith('candidates')
  })

  it('reports the errorChange on a non-failure error when it differs from the change', async () => {
    const onChanged = vi.fn(async () => {})
    scope = effectScope()
    const lifecycle = scope.run(() =>
      useImportLifecycle<'candidates' | 'candidatesAndLayout', string>({
        vacancyId: shallowRef('1'),
        roundId: shallowRef('2'),
        fallback: 'Something went wrong',
        mapMessage: problemMessageText,
        onChanged,
      }),
    )!

    await lifecycle.attempt({
      change: 'candidatesAndLayout',
      errorChange: 'candidates',
      operation: () => Promise.reject(new ApiError(409, { title: 'IntakeRounds.Closed' })),
      announce: () => {},
    })

    expect(onChanged).toHaveBeenCalledWith('candidates')
    expect(onChanged).not.toHaveBeenCalledWith('candidatesAndLayout')
  })

  it('does not refresh on failure', async () => {
    const onChanged = vi.fn(async () => {})
    const { lifecycle } = setup({ onChanged })

    await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(500, { title: 'Server error' })),
      announce: () => {},
    })

    expect(lifecycle.alert.value).toBe('Something went wrong: Please try again.')
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('lets an interception handle an error silently — no alert, no refresh', async () => {
    const onChanged = vi.fn(async () => {})
    const { lifecycle } = setup({ onChanged })

    const result = await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(409, { title: 'Candidates.FormHeaderDrift' })),
      announce: () => {},
      intercept: () => ({ kind: 'handled' }),
    })

    expect(result).toBeNull()
    expect(lifecycle.alert.value).toBeNull()
    expect(onChanged).not.toHaveBeenCalled()
    expect(lifecycle.busy.value).toBe(false)
  })

  it('lets an interception surface its own alert verbatim, without a refresh', async () => {
    const onChanged = vi.fn(async () => {})
    const { lifecycle } = setup({ onChanged })

    await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(400, { detail: 'Row 12 has 7 cells.' })),
      announce: () => {},
      intercept: () => ({ kind: 'alert', alert: "Couldn't read that CSV" }),
    })

    expect(lifecycle.alert.value).toBe("Couldn't read that CSV")
    expect(onChanged).not.toHaveBeenCalled()
  })

  it('falls through to the taxonomy when the interception is unhandled', async () => {
    const { lifecycle } = setup()

    await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(500, { title: 'Server error' })),
      announce: () => {},
      intercept: () => ({ kind: 'unhandled' }),
    })

    expect(lifecycle.alert.value).toBe('Something went wrong: Please try again.')
  })

  it('resets busy and the alert when the vacancy or round changes', async () => {
    const { vacancyId, lifecycle } = setup()

    await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(500, null)),
      announce: () => {},
    })
    expect(lifecycle.alert.value).not.toBeNull()

    vacancyId.value = '99'
    await vi.waitFor(() => {
      expect(lifecycle.alert.value).toBeNull()
    })
    expect(lifecycle.busy.value).toBe(false)
  })

  it('clearAlert dismisses the alert', async () => {
    const { lifecycle } = setup()

    await lifecycle.attempt({
      change: 'candidates',
      operation: () => Promise.reject(new ApiError(500, null)),
      announce: () => {},
    })
    lifecycle.clearAlert()

    expect(lifecycle.alert.value).toBeNull()
  })

  it("runs an attempt on its own busy flag without touching the lifecycle's", async () => {
    const { lifecycle } = setup()
    const submitting = shallowRef(false)
    const gate = deferred<string>()

    const attempt = lifecycle.attempt({
      busy: submitting,
      change: 'candidates',
      operation: () => gate.promise,
      announce: () => {},
    })

    expect(submitting.value).toBe(true)
    expect(lifecycle.busy.value).toBe(false)
    gate.resolve('done')
    await attempt
    expect(submitting.value).toBe(false)
  })
})
