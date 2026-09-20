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
    source: 'none',
  }
}

/** Routes the fetch stub by method + path; each key maps to a response factory. */
function stubSmtpApi(routes: {
  get?: () => Response
  put?: () => Response
  del?: () => Response
  test?: () => Response
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
})
