import { createApp, defineComponent, h } from 'vue'
import { DOMWrapper, flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import ui from '@nuxt/ui/vue-plugin'

import App from '../App.vue'
import { installTechnicalFailureHandling } from './technicalFailure'

const { toastAdd, toastMaxInjectionKey } = vi.hoisted(() => ({
  toastAdd: vi.fn(),
  toastMaxInjectionKey: Symbol('toast-max'),
}))

vi.mock('@nuxt/ui/composables/useToast', () => ({
  useToast: () => ({ add: toastAdd }),
  toastMaxInjectionKey,
}))

const vacanciesRoute = defineComponent({
  setup() {
    return () => h('div', 'Vacancies list')
  },
})

const failingRoute = defineComponent({
  setup() {
    throw new Error('render failed')
  },
  render() {
    return h('div', 'Unreachable route')
  },
})

function createTestRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'vacancy-list', component: vacanciesRoute },
      { path: '/broken', name: 'broken', component: failingRoute },
    ],
  })
}

async function mountAppWithRouter(router: Router, path: string) {
  const app = createApp(App).use(router).use(ui)
  installTechnicalFailureHandling(app, router)
  await router.push(path)
  await router.isReady()

  const root = document.createElement('div')
  document.body.append(root)
  app.mount(root)
  await flushPromises()

  return { app, root }
}

async function mountApp(path: string) {
  return mountAppWithRouter(createTestRouter(), path)
}

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
  document.body.innerHTML = ''
})

describe('Technical Failure', () => {
  it('domain: technical failure keeps the app shell alive around a failed route render', async () => {
    const { app, root } = await mountApp('/broken')

    expect(toastAdd).toHaveBeenCalledWith({
      title: 'Something went wrong',
      description: 'This section hit a technical failure. Your work elsewhere is intact.',
      color: 'error',
    })
    expect(root.textContent).toContain('Something went wrong')
    expect(root.textContent).toContain('Back to Vacancies')
    expect(root.textContent).toContain('Vacancies')

    app.unmount()
  })

  it('domain: technical failure clears when HR returns to the Vacancies list', async () => {
    const { app, root } = await mountApp('/broken')
    const backButton = Array.from(root.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Back to Vacancies'),
    )

    expect(backButton).toBeDefined()
    await new DOMWrapper(backButton as HTMLButtonElement).trigger('click')
    await flushPromises()

    expect(root.textContent).toContain('Vacancies list')
    expect(root.textContent).not.toContain('Back to Vacancies')

    app.unmount()
  })

  it('domain: technical failure from an unhandled rejection leaves the current section rendered', async () => {
    const { app, root } = await mountApp('/')
    toastAdd.mockClear()

    const event = new Event('unhandledrejection')
    Object.defineProperty(event, 'reason', { value: new Error('rejected') })
    window.dispatchEvent(event)
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith({
      title: 'Something went wrong',
      description: 'This section hit a technical failure. Your work elsewhere is intact.',
      color: 'error',
    })
    expect(root.textContent).toContain('Vacancies list')
    expect(root.textContent).not.toContain('Back to Vacancies')

    app.unmount()
  })

  it('domain: technical failure from failed navigation keeps the app shell alive', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', name: 'vacancy-list', component: vacanciesRoute },
        {
          path: '/lazy-broken',
          name: 'lazy-broken',
          component: () => Promise.reject(new Error('navigation failed')),
        },
      ],
    })
    const { app, root } = await mountAppWithRouter(router, '/')
    toastAdd.mockClear()

    await router.push('/lazy-broken').catch(() => undefined)
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith({
      title: 'Something went wrong',
      description: 'This section hit a technical failure. Your work elsewhere is intact.',
      color: 'error',
    })
    expect(root.textContent).toContain('Something went wrong')
    expect(root.textContent).toContain('Back to Vacancies')
    expect(root.textContent).toContain('Vacancies')

    app.unmount()
  })

  it('domain: technical failure remains visible when a later navigation is aborted', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', name: 'vacancy-list', component: vacanciesRoute },
        {
          path: '/lazy-broken',
          name: 'lazy-broken',
          component: () => Promise.reject(new Error('navigation failed')),
        },
        {
          path: '/blocked',
          name: 'blocked',
          component: vacanciesRoute,
          beforeEnter: () => false,
        },
      ],
    })
    const { app, root } = await mountAppWithRouter(router, '/')

    await router.push('/lazy-broken').catch(() => undefined)
    await flushPromises()
    expect(root.textContent).toContain('Something went wrong')

    await router.push('/blocked')
    await flushPromises()

    expect(root.textContent).toContain('Something went wrong')
    expect(root.textContent).toContain('Back to Vacancies')

    app.unmount()
  })
})