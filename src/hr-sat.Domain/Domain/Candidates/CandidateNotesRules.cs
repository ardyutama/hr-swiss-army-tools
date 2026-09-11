using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateNotesRules
{
    public static Result<string?> Replace(string? notes)
    {
        if (notes is not null && notes.Length > 4000)
        {
            return Result<string?>.Failure(NotesTooLong());
        }

        return Result<string?>.Success(string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }

    public static Result<string?> AppendOutcomeNote(string? existingNotes, string? note)
    {
        var normalizedNote = note?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedNote))
        {
            return Result<string?>.Success(existingNotes);
        }

        var updatedNotes = string.IsNullOrWhiteSpace(existingNotes)
            ? normalizedNote
            : $"{existingNotes}\n{normalizedNote}";
        return updatedNotes.Length > 4000
            ? Result<string?>.Failure(NotesTooLong())
            : Result<string?>.Success(updatedNotes);
    }

    private static Error NotesTooLong() => CandidateErrors.Invalid(new Dictionary<string, string[]>
    {
        ["notes"] = ["Notes must be 4000 characters or fewer."]
    });
}