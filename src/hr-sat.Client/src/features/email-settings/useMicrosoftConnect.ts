import { onScopeDispose, shallowRef } from 'vue'
import {
  beginMicrosoftConnect,
  getMicrosoftConnectStatus,
  type MicrosoftConnectChallenge,
} from './api'
import { problemDetailText } from './format'

/**
 * The connect attempt's client-side union (issue 03, decisions 33): the wire states
 * plus `lost` — a poll returning `idle` while the client believed `pending` means the
 * server restarted mid-connect and the attempt is gone.
 */
export type MicrosoftConnectState =
  | { status: 'idle' }
  | { status: 'pending'; challenge: MicrosoftConnectChallenge }
  | { status: 'succeeded'; accountEmail: string }
  | { status: 'failed'; message: string }
  | { status: 'expired' }
  | { status: 'lost' }

const POLL_INTERVAL_MS = 2000

/**
 * The Microsoft Account connect lifecycle (issue 03, decision 26): begin, poll ~2s to
 * a terminal state, reset. One composable per async lifecycle — it never touches the
 * loaded settings; the view reloads them when an attempt succeeds (connect is the
 * test — the server wrote the row already, decision 6).
 */
export function useMicrosoftConnect() {
  const state = shallowRef<MicrosoftConnectState>({ status: 'idle' })
  let pollTimer: ReturnType<typeof setTimeout> | null = null

  function clearPoll() {
    if (pollTimer !== null) {
      clearTimeout(pollTimer)
      pollTimer = null
    }
  }

  async function begin() {
    clearPoll()
    try {
      const challenge = await beginMicrosoftConnect()
      state.value = { status: 'pending', challenge }
      schedulePoll()
    } catch (error) {
      // A refused begin (e.g. sign-in unavailable) lands as the failed state with the
      // server's copy; anything unexpected reads generically.
      state.value = {
        status: 'failed',
        message: problemDetailText(error) ?? 'Could not start the Microsoft sign-in — try again.',
      }
    }
  }

  function schedulePoll() {
    clearPoll()
    pollTimer = setTimeout(poll, POLL_INTERVAL_MS)
  }

  async function poll() {
    try {
      const status = await getMicrosoftConnectStatus()
      switch (status.state) {
        case 'pending':
          schedulePoll()
          return
        case 'succeeded':
          state.value = { status: 'succeeded', accountEmail: status.accountEmail ?? '' }
          return
        case 'failed':
          state.value = {
            status: 'failed',
            message: status.error || 'The sign-in could not be completed — try again.',
          }
          return
        case 'expired':
          state.value = { status: 'expired' }
          return
        default:
          // 'idle' while the client believed pending: the sign-in attempt was lost
          // when the server restarted (decision 33). No client timeout — the server's
          // 'expired' is authoritative.
          state.value = { status: 'lost' }
      }
    } catch {
      // Transient poll errors are swallowed while the attempt is pending server-side
      // (decision 33): keep polling.
      if (state.value.status === 'pending') {
        schedulePoll()
      }
    }
  }

  function reset() {
    clearPoll()
    state.value = { status: 'idle' }
  }

  onScopeDispose(clearPoll)

  return { state, begin, reset }
}
