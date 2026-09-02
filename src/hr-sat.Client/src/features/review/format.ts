import type { CandidateSkill } from '@/features/candidates/api'

/**
 * Requirement Match per CONTEXT.md: an exact match after trimming and
 * case-insensitive comparison. Shared by the requirement ticks and skill chips.
 */
export function normalizePhrase(phrase: string): string {
  return phrase.trim().toLocaleLowerCase()
}

export function matchedSkillIds(
  skills: CandidateSkill[],
  requirementPhrases: string[],
): Set<number> {
  const requirements = new Set(requirementPhrases.map(normalizePhrase))
  return new Set(
    skills
      .filter((skill) => requirements.has(normalizePhrase(skill.phrase)))
      .map((skill) => skill.id),
  )
}

export function requirementMatched(requirementPhrase: string, skills: CandidateSkill[]): boolean {
  const target = normalizePhrase(requirementPhrase)
  return skills.some((skill) => normalizePhrase(skill.phrase) === target)
}

/** HH:MM in the user's locale, 24-hour — for the notes "Saved HH:MM" whisper. */
export function formatClockTime(value: Date): string {
  return value.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}
