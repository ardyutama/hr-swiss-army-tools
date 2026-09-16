using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public static class CandidateErrors
{
    public static Error NotFound(long id) => Error.NotFound(
        "Candidates.NotFound",
        $"Candidate or vacancy with id '{id}' was not found.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("Candidates.Invalid", "The candidate is invalid.", errors);

    public static ValidationError InvalidFormCsv(string message) =>
        new(
            "Candidates.InvalidFormCsv",
            "The form CSV is invalid.",
            new Dictionary<string, string[]>
            {
                ["file"] = [message]
            });

    public static Error SourceEmailAlreadyInRound(IEnumerable<long> candidateIds) => Error.Conflict(
        "Candidates.SourceEmailAlreadyInRound",
        $"The source email for candidate id(s) '{string.Join(", ", candidateIds)}' already exists in the target round.");
}