import { getJson, putJson } from '@/shared/http'

/** A Form Layout role as the client editor reasons about it. */
export type FormLayoutRole = 'name' | 'contactEmail' | 'contactPhone' | 'cvLink'

export const formLayoutRoles: readonly FormLayoutRole[] = [
  'name',
  'contactEmail',
  'contactPhone',
  'cvLink',
]

/** One picked column: an ordinal, optionally bound to a role, optionally labeled. */
export interface FormLayoutColumn {
  ordinal: number
  role: FormLayoutRole | null
  label: string | null
}

export interface FormLayoutDto {
  id: number
  vacancyId: number
  headerSnapshot: string[]
  columns: FormLayoutColumn[]
  isValid: boolean
  candidatesUpdated: number
  typedOverridesKept: number
}

// The wire contract is asymmetric: GET spells roles lowercased
// (`column.Role?.ToString().ToLowerInvariant()` server-side) while PUT and the
// import endpoint's `layout` field deserialize camelCase enum names. Both
// conversions are contained in this module — nothing upstream re-derives them.
interface FormLayoutColumnDto {
  ordinal: number
  role: string | null
  label: string | null
}

interface FormLayoutResponseDto {
  id: number
  vacancyId: number
  headerSnapshot: string[]
  columns: FormLayoutColumnDto[]
  isValid: boolean
  candidatesUpdated: number
  typedOverridesKept: number
}

const rolesFromWire: Record<string, FormLayoutRole> = {
  name: 'name',
  contactemail: 'contactEmail',
  contactphone: 'contactPhone',
  cvlink: 'cvLink',
}

const rolesToWire: Record<FormLayoutRole, string> = {
  name: 'name',
  contactEmail: 'contactEmail',
  contactPhone: 'contactPhone',
  cvLink: 'cvLink',
}

function columnFromWire(dto: FormLayoutColumnDto): FormLayoutColumn {
  return {
    ordinal: dto.ordinal,
    role: dto.role ? (rolesFromWire[dto.role] ?? null) : null,
    label: dto.label,
  }
}

function columnToWire(column: FormLayoutColumn): FormLayoutColumnDto {
  return {
    ordinal: column.ordinal,
    role: column.role ? rolesToWire[column.role] : null,
    label: column.label,
  }
}

function layoutFromWire(dto: FormLayoutResponseDto): FormLayoutDto {
  return {
    id: dto.id,
    vacancyId: dto.vacancyId,
    headerSnapshot: dto.headerSnapshot,
    columns: dto.columns.map(columnFromWire),
    isValid: dto.isValid,
    candidatesUpdated: dto.candidatesUpdated,
    typedOverridesKept: dto.typedOverridesKept,
  }
}

/** Throws ApiError 404 (`FormLayouts.NotFound`) when the vacancy has no layout yet. */
export async function getFormLayout(vacancyId: string): Promise<FormLayoutDto> {
  const dto = await getJson<FormLayoutResponseDto>(`/api/vacancies/${vacancyId}/form-layout`)
  return layoutFromWire(dto)
}

export async function saveFormLayout(
  vacancyId: string,
  columns: FormLayoutColumn[],
): Promise<FormLayoutDto> {
  const dto = await putJson<FormLayoutResponseDto>(`/api/vacancies/${vacancyId}/form-layout`, {
    columns: columns.map(columnToWire),
  })
  return layoutFromWire(dto)
}

/**
 * Serializes the mapping for the import endpoint's `layout` multipart field:
 * one JSON document the server deserializes as the Form Layout definition.
 * The import-form feature's api module appends it to its FormData.
 */
export function serializeLayoutColumns(columns: FormLayoutColumn[]): string {
  return JSON.stringify({ columns: columns.map(columnToWire) })
}
