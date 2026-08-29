export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: unknown,
  ) {
    super(`API request failed with status ${status}`)
  }
}

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new ApiError(response.status, await response.json().catch(() => null))
  }
  return (await response.json()) as T
}

async function sendJson<T>(
  path: string,
  method: 'POST' | 'PUT' | 'DELETE',
  body?: unknown,
): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    throw new ApiError(response.status, await response.json().catch(() => null))
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

export function postJson<T>(path: string, body: unknown): Promise<T> {
  return sendJson<T>(path, 'POST', body)
}

export function putJson<T>(path: string, body: unknown): Promise<T> {
  return sendJson<T>(path, 'PUT', body)
}

export function delJson(path: string): Promise<void> {
  return sendJson<void>(path, 'DELETE')
}
