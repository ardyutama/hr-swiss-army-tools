namespace hr_sat.Domain.Candidates;

// The name a message addresses the candidate by: the typed name, else the source
// sender name, else a neutral "there" (CONTEXT.md, Email Template).
public static class CandidateDisplayName
{
    public static string Resolve(string? fullName, string? sourceSenderName) =>
        !string.IsNullOrWhiteSpace(fullName)
            ? fullName.Trim()
            : !string.IsNullOrWhiteSpace(sourceSenderName)
                ? sourceSenderName.Trim()
                : "there";
}
