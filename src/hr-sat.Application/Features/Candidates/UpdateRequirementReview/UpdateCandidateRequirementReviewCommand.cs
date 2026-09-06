using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateRequirementReview;

public sealed record UpdateCandidateRequirementReviewCommand(
    long VacancyId,
    long CandidateId,
    long RequirementId,
    bool Confirmed) : ICommand<CandidateDetailsResponse>;
