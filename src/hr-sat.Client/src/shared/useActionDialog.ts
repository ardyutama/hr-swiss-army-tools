import { shallowRef, watch } from 'vue'

export type ActionDialogResult = string | null | boolean

type ActionDialogOptions = {
  onReset?: () => void
}

export function useActionDialog<TPayload = null>(options: ActionDialogOptions = {}) {
  const open = shallowRef(false)
  const payload = shallowRef<TPayload | null>(null)
  const error = shallowRef<string | null>(null)

  watch(open, (isOpen) => {
    if (!isOpen) {
      payload.value = null
      error.value = null
      options.onReset?.()
    }
  }, { flush: 'sync' })

  function request(nextPayload?: TPayload) {
    payload.value = nextPayload ?? null
    error.value = null
    open.value = true
  }

  async function confirm(
    action: (currentPayload: TPayload | null) => Promise<ActionDialogResult>,
  ): Promise<ActionDialogResult> {
    const result = await action(payload.value)
    if (result === null || result === true) {
      open.value = false
    } else if (typeof result === 'string') {
      error.value = result
    }
    return result
  }

  function dismiss() {
    open.value = false
  }

  return { open, payload, error, request, confirm, dismiss }
}