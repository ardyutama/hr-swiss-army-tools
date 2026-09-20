/** Column-select option text: `ordinal · label` when a Column Label exists, else `ordinal · header` truncated (~48 chars). */
export function columnOptionLabel(ordinal: number, header: string, label: string | null): string {
  const text = label ?? truncateHeader(header, 48)
  return `${ordinal} · ${text}`
}

function truncateHeader(text: string, maxLength: number): string {
  return text.length <= maxLength ? text : `${text.slice(0, maxLength - 1)}…`
}
