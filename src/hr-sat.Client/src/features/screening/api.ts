import { getJson, postJson, putJson } from '@/shared/http'

/** Screening operators travel as kebab-case strings both ways. */
export type ScreeningOperator = 'equals' | 'not-equals' | 'is-empty' | 'not-empty' | 'contains'

export interface ScreeningRule {
  ordinal: number
  operator: ScreeningOperator
  value: string | null
}

export interface ScreeningRuleSet {
  id: number
  vacancyId: number
  rules: ScreeningRule[]
}

/** The live preview of a draft rule set over the active round's form candidates. */
export interface ScreeningRulesPreview {
  total: number
  screenedOut: number
  perRule: { index: number; screenedOut: number }[]
}

/** Throws ApiError 404 when the vacancy has no rule set yet — the client's empty state. */
export function getScreeningRules(vacancyId: string): Promise<ScreeningRuleSet> {
  return getJson<ScreeningRuleSet>(`/api/vacancies/${vacancyId}/screening-rules`)
}

export function saveScreeningRules(
  vacancyId: string,
  rules: ScreeningRule[],
): Promise<ScreeningRuleSet> {
  return putJson<ScreeningRuleSet>(`/api/vacancies/${vacancyId}/screening-rules`, { rules })
}

export function previewScreeningRules(
  vacancyId: string,
  rules: ScreeningRule[],
): Promise<ScreeningRulesPreview> {
  return postJson<ScreeningRulesPreview>(
    `/api/vacancies/${vacancyId}/screening-rules/preview`,
    { rules },
  )
}
