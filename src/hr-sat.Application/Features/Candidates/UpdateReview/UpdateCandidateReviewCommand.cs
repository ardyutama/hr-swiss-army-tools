using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates;

namespace hr_sat.Application.Features.Candidates.UpdateReview;

public sealed record UpdateCandidateReviewCommand(
    long VacancyId,
    long CandidateId,
    string? ReviewStatus,
    string? Notes) : ICommand<CandidateDetailsResponse>;
