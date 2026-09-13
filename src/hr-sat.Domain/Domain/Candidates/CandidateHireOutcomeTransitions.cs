using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateHireOutcomeTransitions
{
    public static Result<CandidateHireOutcomeTransition> Calculate(
        CandidateHireOutcome currentOutcome,
        CandidateHireOutcome requestedOutcome,
        string? existingNotes,
        string? note)
    {
        var isAllowed = requestedOutcome switch
        {
            CandidateHireOutcome.None => true,
            CandidateHireOutcome.Hired => currentOutcome is
                CandidateHireOutcome.None or CandidateHireOutcome.Declined or CandidateHireOutcome.Runaway,
            CandidateHireOutcome.Runaway => currentOutcome == CandidateHireOutcome.Hired,
            CandidateHireOutcome.Declined => currentOutcome == CandidateHireOutcome.None,
            _ => false
        };
        if (!isAllowed)
        {
            return Result<CandidateHireOutcomeTransition>.Failure(CandidateErrors.Invalid(
                new Dictionary<string, string[]>
                {
                    ["hireOutcome"] = ["The requested hire outcome transition is not allowed."]
                }));
        }

        var notesResult = CandidateNotesRules.AppendOutcomeNote(existingNotes, note);
        if (notesResult.IsFailure)
        {
            return Result<CandidateHireOutcomeTransition>.Failure(notesResult.Error);
        }

        return Result<CandidateHireOutcomeTransition>.Success(
            new CandidateHireOutcomeTransition(requestedOutcome, notesResult.Value));
    }
}