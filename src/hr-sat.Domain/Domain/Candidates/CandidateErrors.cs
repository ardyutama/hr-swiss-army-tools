using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public static class CandidateErrors
{
    public static Error NotFound(long id) => Error.NotFound(
        "Candidates.NotFound",
        $"Candidate or vacancy with id '{id}' was not found.");

    public static ValidationError Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new("Candidates.Invalid", "The candidate is invalid.", errors);
}