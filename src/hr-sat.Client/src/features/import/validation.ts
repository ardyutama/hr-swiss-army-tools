export interface RejectedImportFile {
  name: string
  reason: string
}

export type ImportFileDispatch =
  | { kind: 'eml'; files: File[] }
  | { kind: 'csv'; file: File }
  | { kind: 'rejected'; rejected: RejectedImportFile[] }

const NOT_AN_IMPORT_FILE = "isn't an .eml or a .csv export"
const DROP_EML_SEPARATELY = 'drop .eml files on their own'
const DROP_ONE_CSV = 'drop one .csv export on its own'

/**
 * Routes a drop by extension: a homogeneous .eml batch goes to the email
 * import, a single Google Forms .csv export to the form import. Anything else
 * (mixed, multi-.csv, wrong types) rejects the whole drop — nothing uploads.
 */
export function classifyImportFiles(files: File[]): ImportFileDispatch {
  const emlFiles = files.filter((file) => hasExtension(file, '.eml'))
  const csvFiles = files.filter((file) => hasExtension(file, '.csv'))
  const unsupported = files.filter(
    (file) => !hasExtension(file, '.eml') && !hasExtension(file, '.csv'),
  )

  if (unsupported.length === 0) {
    if (csvFiles.length === 0 && emlFiles.length > 0) {
      return { kind: 'eml', files: emlFiles }
    }
    if (emlFiles.length === 0 && csvFiles.length === 1) {
      return { kind: 'csv', file: csvFiles[0]! }
    }
  }

  const rejected: RejectedImportFile[] = files.map((file) => {
    if (unsupported.includes(file)) {
      return { name: file.name, reason: NOT_AN_IMPORT_FILE }
    }
    if (hasExtension(file, '.csv')) {
      return { name: file.name, reason: DROP_ONE_CSV }
    }
    return { name: file.name, reason: DROP_EML_SEPARATELY }
  })
  return { kind: 'rejected', rejected }
}

function hasExtension(file: File, extension: string): boolean {
  return file.name.toLowerCase().endsWith(extension)
}
