import { effectScope, nextTick } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useActionDialog } from './useActionDialog'

describe('useActionDialog', () => {
  let scope: ReturnType<typeof effectScope> | null = null

  afterEach(() => {
    scope?.stop()
    scope = null
  })

  it('captures a payload and resets it with the error when dismissed', async () => {
    const onReset = vi.fn()
    scope = effectScope()
    const dialog = scope.run(() => useActionDialog<string>({ onReset }))!

    dialog.request('candidate-7')
    dialog.error.value = 'A previous failure'
    dialog.dismiss()
    await nextTick()

    expect(dialog.open.value).toBe(false)
    expect(dialog.payload.value).toBeNull()
    expect(dialog.error.value).toBeNull()
    expect(onReset).toHaveBeenCalledOnce()
  })

  it('resets before a dismissed dialog can be requested again in the same tick', () => {
    const onReset = vi.fn()
    scope = effectScope()
    const dialog = scope.run(() => useActionDialog<string>({ onReset }))!

    dialog.request('candidate-7')
    dialog.error.value = 'A previous failure'
    dialog.dismiss()
    dialog.request('candidate-8')

    expect(dialog.open.value).toBe(true)
    expect(dialog.payload.value).toBe('candidate-8')
    expect(dialog.error.value).toBeNull()
    expect(onReset).toHaveBeenCalledOnce()
  })

  it('passes the payload to the action and closes after a successful result', async () => {
    const onReset = vi.fn()
    const action = vi.fn().mockResolvedValue(true)
    scope = effectScope()
    const dialog = scope.run(() => useActionDialog<string>({ onReset }))!

    dialog.request('candidate-7')
    const result = await dialog.confirm(action)
    await nextTick()

    expect(result).toBe(true)
    expect(action).toHaveBeenCalledWith('candidate-7')
    expect(dialog.open.value).toBe(false)
    expect(dialog.payload.value).toBeNull()
    expect(dialog.error.value).toBeNull()
    expect(onReset).toHaveBeenCalledOnce()
  })

  it.each([null, false])('handles a %s action result without closing incorrectly', async (result) => {
    scope = effectScope()
    const dialog = scope.run(() => useActionDialog<string>())!
    dialog.request('candidate-7')

    await dialog.confirm(async () => result)
    await nextTick()

    expect(dialog.open.value).toBe(result === false)
    expect(dialog.error.value).toBeNull()
  })

  it('keeps the dialog open and exposes a returned error string', async () => {
    scope = effectScope()
    const dialog = scope.run(() => useActionDialog())!
    dialog.request()

    const result = await dialog.confirm(async () => 'Candidate could not be removed')
    await nextTick()

    expect(result).toBe('Candidate could not be removed')
    expect(dialog.open.value).toBe(true)
    expect(dialog.error.value).toBe('Candidate could not be removed')
  })
})