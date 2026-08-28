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
