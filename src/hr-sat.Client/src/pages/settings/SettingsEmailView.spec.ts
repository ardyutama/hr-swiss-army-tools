import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'

import SettingsEmailView from './SettingsEmailView.vue'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function smtpSettings(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    host: 'smtp.gmail.com',
    port: 587,
    username: 'hr@firma.example',
    fromAddress: 'hr@firma.example',
    fromName: 'HR Team',
    hasPassword: true,
    signInMethod: 'app-password',
    microsoftSignInAvailable: true,
    source: 'settings',
    ...overrides,
  }
}

/** A connected Microsoft Account as the GET reports it (issue 03, decision 10). */
function microsoftSettings(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    host: 'smtp.office365.com',
    port: 587,
    username: 'anna@outlook.com',
    fromAddress: 'anna@outlook.com',
    fromName: 'Anna HR',
    hasPassword: false,
    signInMethod: 'microsoft-account',
    microsoftSignInAvailable: true,
    source: 'settings',
    ...overrides,
  }
}

function noSettings() {
  return {
    host: null,
    port: null,
    username: null,
    fromAddress: null,
    fromName: null,
    hasPassword: false,
    signInMethod: null,
    microsoftSignInAvailable: true,
    source: 'none',
  }
}

/** Routes the fetch stub by method + path; each key maps to a response factory. */
function stubSmtpApi(routes: {
  get?: () => Response
  put?: () => Response
  del?: () => Response
  test?: () => Response
  begin?: () => Response
  status?: () => Response
}) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input)
    const method = init?.method ?? 'GET'
    if (url === '/api/settings/smtp' && method === 'GET' && routes.get) {
      return Promise.resolve(routes.get())
    }
    if (url === '/api/settings/smtp' && method === 'PUT' && routes.put) {
      return Promise.resolve(routes.put())
    }
    if (url === '/api/settings/smtp' && method === 'DELETE' && routes.del) {
      return Promise.resolve(routes.del())
    }
    if (url === '/api/settings/smtp/test' && method === 'POST' && routes.test) {
      return Promise.resolve(routes.test())
    }
    if (url === '/api/settings/smtp/microsoft-connect/begin' && method === 'POST' && routes.begin) {
      return Promise.resolve(routes.begin())
    }
    if (url === '/api/settings/smtp/microsoft-connect/status' && method === 'GET' && routes.status) {
      return Promise.resolve(routes.status())
    }
    throw new Error(`unexpected fetch ${method} ${url}`)
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

function fieldInput(wrapper: VueWrapper, labelText: string): HTMLInputElement {
  const root = wrapper.element as HTMLElement
  const label = Array.from(root.querySelectorAll('label')).find((item) =>
    item.textContent?.includes(labelText),
  )
  const id = label?.getAttribute('for')
  const input = id ? root.querySelector<HTMLInputElement>(`#${id}`) : null
  if (!input) {
    throw new Error(`no input found for label '${labelText}'`)
  }
  return input
}

async function setField(wrapper: VueWrapper, labelText: string, value: string) {
  const input = fieldInput(wrapper, labelText)
  await wrapper.find(`#${input.id}`).setValue(value)
}

function button(scope: ParentNode, text: string): HTMLButtonElement {
  const match = Array.from(scope.querySelectorAll('button')).find((item) =>
    item.textContent?.includes(text),
  )
  if (!match) {
    throw new Error(`no button containing '${text}'`)
  }
  return match
}

/** URadioGroup items render as button[role="radio"] (Reka); match by item label. */
function radioItem(scope: ParentNode, text: string): HTMLElement {
  const match = Array.from(scope.querySelectorAll('[role="radio"]')).find((item) =>
    item.textContent?.includes(text),
  )
  if (!match) {
    throw new Error(`no radio item containing '${text}'`)
  }
  return match as HTMLElement
}

/** The Sign-in Method row is progressive disclosure — present only for Outlook (decision 25). */
function hasSignInMethodRow(wrapper: VueWrapper): boolean {
  return Array.from((wrapper.element as HTMLElement).querySelectorAll('label')).some(
    (item) => item.textContent?.includes('Sign-in method'),
  )
}

/** Selects the Outlook preset and the Microsoft Account method through the radios. */
async function selectMicrosoftMethod(wrapper: VueWrapper) {
  radioItem(wrapper.element, 'Outlook').click()
  await flushPromises()
  radioItem(wrapper.element, 'Microsoft account').click()
  await flushPromises()
}

const connectChallenge = {
  userCode: 'ABCD-EFGH',
  verificationUrl: 'https://microsoft.com/link',
  expiresAt: '2026-09-21T12:15:00Z',
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('SettingsEmailView', () => {
  it('domain: SMTP Account — an unconfigured installation renders the form with the neutral status line', async () => {
    stubSmtpApi({ get: () => jsonResponse(noSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    expect(wrapper.text()).toContain('Email sending')
    expect(wrapper.text()).toContain('Email sending is not configured.')
    // Gmail is the default preset: its host and port come pre-filled (decision 6).
    expect(fieldInput(wrapper, 'Host').value).toBe('smtp.gmail.com')
    expect(fieldInput(wrapper, 'Port').value).toBe('587')
    expect(fieldInput(wrapper, 'Password').placeholder).toBe('Enter app password')
    expect(button(wrapper.element, 'Save').disabled).toBe(true)
    // The escape hatch renders only for a saved row (decision 11).
    expect(wrapper.text()).not.toContain('Remove saved settings')
    // The Sign-in Method row stays hidden off the Outlook preset (decision 25).
    expect(hasSignInMethodRow(wrapper)).toBe(false)
  })

  it('domain: SMTP Account — HR tests the typed values, then saves, and the status line updates in place', async () => {
    stubSmtpApi({
      get: () => jsonResponse(noSettings()),
      test: () => jsonResponse({ message: 'Connected and authenticated.' }),
      put: () => jsonResponse(smtpSettings()),
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    await setField(wrapper, 'Username', 'hr@firma.example')
    await setField(wrapper, 'Password', 'app-password')
    await setField(wrapper, 'From address', 'hr@firma.example')

    expect(button(wrapper.element, 'Save').disabled).toBe(true)

    await button(wrapper.element, 'Test connection').click()
    await flushPromises()

    expect(wrapper.text()).toContain('Connected and authenticated.')
    expect(button(wrapper.element, 'Save').disabled).toBe(false)

    // Any edit after a pass returns the form to must-test-again (decision 3).
    await setField(wrapper, 'From name', 'HR Team')
    expect(button(wrapper.element, 'Save').disabled).toBe(true)

    await button(wrapper.element, 'Test connection').click()
    await flushPromises()
    await button(wrapper.element, 'Save').click()
    await flushPromises()

    // The status line updating in place is the success feedback — no toast (decision 10).
    expect(wrapper.text()).toContain(
      'Sending as hr@firma.example via smtp.gmail.com:587 — saved on this page.',
    )
    expect(fieldInput(wrapper, 'Password').placeholder).toBe('Saved — type to replace')
    expect(fieldInput(wrapper, 'Password').value).toBe('')
  })

  it('domain: SMTP Account — a failed test shows the server message inline and keeps Save locked', async () => {
    stubSmtpApi({
      get: () => jsonResponse(noSettings()),
      test: () =>
        jsonResponse(
          {
            title: 'EmailSettings.TestFailed',
            detail: 'Authentication failed — check the app password.',
            status: 400,
          },
          400,
        ),
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    await setField(wrapper, 'Username', 'hr@firma.example')
    await setField(wrapper, 'Password', 'wrong-password')
    await setField(wrapper, 'From address', 'hr@firma.example')
    await button(wrapper.element, 'Test connection').click()
    await flushPromises()

    expect(wrapper.text()).toContain('Authentication failed — check the app password.')
    expect(button(wrapper.element, 'Save').disabled).toBe(true)
    // Nothing was persisted: the probe never saves (decision 3).
    expect(wrapper.text()).toContain('Email sending is not configured.')
  })

  it('domain: SMTP Account — removing the saved settings returns to the configuration file fallback', async () => {
    let current = smtpSettings()
    stubSmtpApi({
      get: () => jsonResponse(current),
      del: () => {
        current = smtpSettings({
          host: 'smtp.test.invalid',
          fromName: null,
          source: 'configuration-file',
        })
        return new Response(null, { status: 204 })
      },
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    expect(wrapper.text()).toContain(
      'Sending as hr@firma.example via smtp.gmail.com:587 — saved on this page.',
    )

    await button(wrapper.element, 'Remove saved settings').click()
    await flushPromises()

    // The confirm dialog is teleported to the body.
    expect(document.body.textContent).toContain(
      'Remove the saved SMTP settings? The app will fall back to the configuration file if it exists.',
    )
    await button(document.body, 'Remove').click()
    await flushPromises()

    expect(wrapper.text()).toContain(
      'Sending as hr@firma.example via smtp.test.invalid:587 — from the configuration file.',
    )
    expect(wrapper.text()).not.toContain('Remove saved settings')
  })

  it('domain: Sign-in Method — the method row appears only for the Outlook preset (decision 25)', async () => {
    stubSmtpApi({ get: () => jsonResponse(noSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    expect(hasSignInMethodRow(wrapper)).toBe(false)

    radioItem(wrapper.element, 'Outlook').click()
    await flushPromises()
    expect(hasSignInMethodRow(wrapper)).toBe(true)

    radioItem(wrapper.element, 'Gmail').click()
    await flushPromises()
    expect(hasSignInMethodRow(wrapper)).toBe(false)
  })

  it('domain: Sign-in Method — the Outlook preset recommends the Microsoft Account sign-in (decision 36)', async () => {
    stubSmtpApi({ get: () => jsonResponse(noSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    radioItem(wrapper.element, 'Outlook').click()
    await flushPromises()

    expect(wrapper.text()).toContain(
      'Microsoft Account sign-in recommended — personal Outlook retired app passwords.',
    )
  })

  it('domain: Sign-in Method — switching to Microsoft account swaps the credential fields for the connect panel (decision 10)', async () => {
    stubSmtpApi({ get: () => jsonResponse(noSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    await selectMicrosoftMethod(wrapper)

    // The credential fieldset is gone; the connect panel offers the sign-in.
    expect(
      Array.from((wrapper.element as HTMLElement).querySelectorAll('label')).some(
        (item) => item.textContent?.includes('Password'),
      ),
    ).toBe(false)
    expect(wrapper.text()).toContain('Sign in with the Microsoft account this app sends from.')
    // The From fields stay visible (decision 10).
    expect(fieldInput(wrapper, 'From address')).toBeTruthy()

    // Back to App Password (never connected, so no confirm): the fieldset returns.
    radioItem(wrapper.element, 'App password').click()
    await flushPromises()
    expect(fieldInput(wrapper, 'Password')).toBeTruthy()
  })

  it('domain: Sign-in Method — connecting shows the code, then "Connected as", and unlocks Save (decisions 33, 35)', async () => {
    let settingsPayload: Record<string, unknown> = noSettings()
    let statusCalls = 0
    stubSmtpApi({
      get: () => jsonResponse(settingsPayload),
      begin: () => jsonResponse(connectChallenge),
      status: () => {
        statusCalls += 1
        if (statusCalls < 2) {
          return jsonResponse({ state: 'pending' })
        }
        // Connect is the test (decision 6): the row is already written server-side,
        // so the reload reports the connected account.
        settingsPayload = microsoftSettings()
        return jsonResponse({ state: 'succeeded', accountEmail: 'anna@outlook.com' })
      },
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()
    await selectMicrosoftMethod(wrapper)

    // Not connected yet: Save stays locked (decision 35).
    expect(button(wrapper.element, 'Save').disabled).toBe(true)

    button(wrapper.element, 'Connect Microsoft account').click()
    await flushPromises()

    expect(wrapper.text()).toContain('Enter this code at')
    expect(wrapper.text()).toContain('microsoft.com/link')
    expect(wrapper.text()).toContain('ABCD-EFGH')
    expect(wrapper.text()).toContain('Waiting for sign-in — the code expires at')

    vi.useFakeTimers()
    try {
      // First poll: still pending; second poll: succeeded — then the view reloads.
      await vi.advanceTimersByTimeAsync(2100)
      await flushPromises()
      await vi.advanceTimersByTimeAsync(2100)
      await flushPromises()

      expect(wrapper.text()).toContain('Connected as anna@outlook.com.')
      expect(button(wrapper.element, 'Save').disabled).toBe(false)
    } finally {
      vi.useRealTimers()
    }
  })

  it('domain: Sign-in Method — a declined or expired sign-in renders inline with a retry (decision 33)', async () => {
    const statusQueue = [
      { state: 'failed', error: 'The sign-in was declined before it finished.' },
      { state: 'expired' },
    ]
    stubSmtpApi({
      get: () => jsonResponse(noSettings()),
      begin: () => jsonResponse(connectChallenge),
      status: () => jsonResponse(statusQueue.shift() ?? { state: 'idle' }),
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()
    await selectMicrosoftMethod(wrapper)

    button(wrapper.element, 'Connect Microsoft account').click()
    await flushPromises()

    vi.useFakeTimers()
    try {
      await vi.advanceTimersByTimeAsync(2100)
      await flushPromises()
      expect(wrapper.text()).toContain('The sign-in was declined before it finished.')

      // Try again re-begins the flow; the follow-up attempt expires.
      // (Native click: the button rendered inside the fake-timer window.)
      button(wrapper.element, 'Try again').click()
      await flushPromises()
      expect(wrapper.text()).toContain('Enter this code at')

      await vi.advanceTimersByTimeAsync(2100)
      await flushPromises()
      expect(wrapper.text()).toContain('The code expired before sign-in finished.')
      expect(button(wrapper.element, 'Get a new code')).toBeTruthy()
    } finally {
      vi.useRealTimers()
    }
  })

  it('domain: Sign-in Method — a server restart mid-connect surfaces the lost attempt (decision 33)', async () => {
    stubSmtpApi({
      get: () => jsonResponse(noSettings()),
      begin: () => jsonResponse(connectChallenge),
      // 'idle' while the client believed pending: the attempt is gone (process restart).
      status: () => jsonResponse({ state: 'idle' }),
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()
    await selectMicrosoftMethod(wrapper)

    button(wrapper.element, 'Connect Microsoft account').click()
    await flushPromises()

    vi.useFakeTimers()
    try {
      await vi.advanceTimersByTimeAsync(2100)
      await flushPromises()
      expect(wrapper.text()).toContain(
        'The sign-in attempt was lost when the server restarted — start again.',
      )
    } finally {
      vi.useRealTimers()
    }
  })

  it('domain: Sign-in Method — a saved Microsoft account renders Connected with editable From fields and a gated save (decisions 10, 35)', async () => {
    stubSmtpApi({
      get: () => jsonResponse(microsoftSettings()),
      put: () => jsonResponse(microsoftSettings({ fromName: 'Anna HR!' })),
    })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    // The connected state, the Microsoft status line, and the relabeled escape hatch.
    expect(wrapper.text()).toContain('Connected as anna@outlook.com.')
    expect(wrapper.text()).toContain(
      'Sending as anna@outlook.com via Microsoft account — saved on this page.',
    )
    expect(wrapper.text()).toContain('Disconnect…')
    expect(wrapper.text()).not.toContain('Test connection')

    // The credential fields never render; From stays editable (decision 10).
    expect(
      Array.from((wrapper.element as HTMLElement).querySelectorAll('label')).some(
        (item) => item.textContent?.includes('Password'),
      ),
    ).toBe(false)
    expect(fieldInput(wrapper, 'From address').value).toBe('anna@outlook.com')

    // Connected with valid From fields: Save is enabled, and editing only the From
    // name keeps it enabled (decision 35).
    expect(button(wrapper.element, 'Save').disabled).toBe(false)
    await setField(wrapper, 'From name', 'Anna HR!')
    expect(button(wrapper.element, 'Save').disabled).toBe(false)
  })

  it('domain: Sign-in Method — switching away from a connected account confirms first and reverts on cancel (decision 34)', async () => {
    stubSmtpApi({ get: () => jsonResponse(microsoftSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    radioItem(wrapper.element, 'App password').click()
    await flushPromises()

    // The confirm names the account it would discard; nothing changes until Save.
    expect(document.body.textContent).toContain('Switch to App Password?')
    expect(document.body.textContent).toContain(
      'Saving after switching disconnects anna@outlook.com — the Microsoft sign-in is discarded when you save. Nothing changes until then.',
    )

    button(document.body, 'Keep Microsoft account').click()
    await flushPromises()
    expect(document.body.textContent).not.toContain('Switch to App Password?')
    // Reverted: still Microsoft, still connected — the credential fieldset stays hidden.
    expect(wrapper.text()).toContain('Connected as anna@outlook.com.')

    radioItem(wrapper.element, 'App password').click()
    await flushPromises()
    button(document.body, 'Switch to App Password').click()
    await flushPromises()

    // Confirmed: the credential fieldset returns (the grant is discarded on Save).
    expect(fieldInput(wrapper, 'Password')).toBeTruthy()
  })

  it('domain: Sign-in Method — leaving the Outlook preset while Microsoft is selected confirms first (decision 34)', async () => {
    stubSmtpApi({ get: () => jsonResponse(microsoftSettings()) })

    const wrapper = mount(SettingsEmailView)
    await flushPromises()

    radioItem(wrapper.element, 'Gmail').click()
    await flushPromises()
    expect(document.body.textContent).toContain('Switch to App Password?')

    button(document.body, 'Keep Microsoft account').click()
    await flushPromises()

    // Reverted: the Outlook preset and the connected Microsoft account stand.
    expect(wrapper.text()).toContain('Connected as anna@outlook.com.')
    expect(hasSignInMethodRow(wrapper)).toBe(true)
  })
})
