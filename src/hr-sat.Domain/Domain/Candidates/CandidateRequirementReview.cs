using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class CandidateRequirementReview : Entity
{
    private CandidateRequirementReview()
    {
    }

    internal CandidateRequirementReview(
        long candidateId,
        long vacancyRequirementId,
        bool confirmed)
    {
        CandidateId = candidateId;
        VacancyRequirementId = vacancyRequirementId;
        Confirmed = confirmed;
    }

    public long CandidateId { get; private set; }
    public long VacancyRequirementId { get; private set; }
    public bool Confirmed { get; private set; }

    internal void SetConfirmed(bool confirmed) => Confirmed = confirmed;
}
