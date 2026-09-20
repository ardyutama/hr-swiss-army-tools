namespace hr_sat.Application.Features.Dispatches;

public sealed record DispatchExclusionResponse(
    long CandidateId,
    string CandidateName,
    string Reason);
