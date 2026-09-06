import { describe, expect, it } from 'vitest'

import type { VacancyHiring } from './api'
import { hiringShortage, isFilled } from './hiring'

function hiring(overrides: Partial<VacancyHiring> = {}): VacancyHiring {
  return { neededHires: 4, activeHires: 1, ...overrides }
}

describe('domain: vacancy Shortage and Filled', () => {
  it('counts the Needed Hires slots that remain open', () => {
    expect(hiringShortage(hiring())).toBe(3)
    expect(hiringShortage(hiring({ neededHires: 2, activeHires: 4 }))).toBe(0)
  })

  it('considers a vacancy Filled when active hires reach the target', () => {
    expect(isFilled(hiring({ neededHires: 4, activeHires: 3 }))).toBe(false)
    expect(isFilled(hiring({ neededHires: 4, activeHires: 4 }))).toBe(true)
    expect(isFilled(hiring({ neededHires: 4, activeHires: 5 }))).toBe(true)
  })

})
