import type { VacancyHiring } from './api'

export function hiringShortage(hiring: VacancyHiring): number {
  return Math.max(0, hiring.neededHires - hiring.activeHires)
}

export function isFilled(hiring: VacancyHiring): boolean {
  return hiring.activeHires >= hiring.neededHires
}

export function hiringProgressText(hiring: VacancyHiring): string {
  return `${hiring.activeHires}/${hiring.neededHires} hired \u00b7 ${hiringShortage(hiring)} to go`
}
