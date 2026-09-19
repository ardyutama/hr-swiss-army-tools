using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;

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

    public static Error FormLayoutRequired(
        long vacancyId,
        IReadOnlyList<string> headers) => Error.Conflict(
        "Candidates.FormLayoutRequired",
        $"Vacancy '{vacancyId}' must have a valid Form Layout with Name and Contact Email bindings before form responses can be imported.",
        new Dictionary<string, object?>
        {
            ["headers"] = headers
        });

    public static Error FormHeaderDrift(
        IReadOnlyList<string> headers,
        IReadOnlyList<FormLayoutHeaderChange> changes) => Error.Conflict(
        "Candidates.FormHeaderDrift",
        "The uploaded form headers differ from the saved Form Layout.",
        new Dictionary<string, object?>
        {
            ["headers"] = headers,
            ["changes"] = changes
        });

    public static Error SourceEmailAlreadyInRound(IEnumerable<long> candidateIds) => Error.Conflict(
        "Candidates.SourceEmailAlreadyInRound",
        $"The source email for candidate id(s) '{string.Join(", ", candidateIds)}' already exists in the target round.");

    public static Error ScreenedOut(
        long candidateId,
        IReadOnlyList<ScreeningRuleMatch> firedRules) => Error.Conflict(
        "Candidates.ScreenedOut",
        $"Candidate '{candidateId}' is currently screened out by: {string.Join(", ", firedRules.Select(rule => rule.Display))}.",
        new Dictionary<string, object?>
        {
            ["firedRules"] = firedRules
        });
}