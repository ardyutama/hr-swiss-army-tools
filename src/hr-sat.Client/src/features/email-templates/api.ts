import { delJson, getJson, postJson, putJson } from '@/shared/http'

export type EmailTemplateKind = 'shortlisted' | 'rejected'

/** Editable subject + body text owned by one vacancy for one kind (glossary: Email Template). */
export interface EmailTemplate {
  kind: EmailTemplateKind
  subject: string
  body: string
}

/** Both per-vacancy templates; a kind is null until HR creates it. */
export interface VacancyEmailTemplates {
  shortlisted: EmailTemplate | null
  rejected: EmailTemplate | null
}

/** One other vacancy owning a template of the requested kind (copy-from source). */
export interface TemplateSource {
  vacancyId: string
  vacancyTitle: string
  openedOn: string
  subject: string
  body: string
}

export interface EmailTemplateWritePayload {
  subject: string
  body: string
}

/** A template rendered for one candidate — the Prepared Message. */
export interface RenderedMessage {
  subject: string
  body: string
}

export function listEmailTemplates(vacancyId: string): Promise<VacancyEmailTemplates> {
  return getJson<VacancyEmailTemplates>(`/api/vacancies/${vacancyId}/email-templates`)
}

export function upsertEmailTemplate(
  vacancyId: string,
  kind: EmailTemplateKind,
  payload: EmailTemplateWritePayload,
): Promise<EmailTemplate> {
  return putJson<EmailTemplate>(`/api/vacancies/${vacancyId}/email-templates/${kind}`, payload)
}

export function deleteEmailTemplate(vacancyId: string, kind: EmailTemplateKind): Promise<void> {
  return delJson(`/api/vacancies/${vacancyId}/email-templates/${kind}`)
}

export function listTemplateSources(
  vacancyId: string,
  kind: EmailTemplateKind,
): Promise<TemplateSource[]> {
  return getJson<TemplateSource[]>(
    `/api/vacancies/${vacancyId}/email-templates/sources?kind=${kind}`,
  )
}

export function renderEmailTemplate(
  vacancyId: string,
  payload: { subject: string; body: string; candidateId: number },
): Promise<RenderedMessage> {
  return postJson<RenderedMessage>(`/api/vacancies/${vacancyId}/email-templates/render`, payload)
}
