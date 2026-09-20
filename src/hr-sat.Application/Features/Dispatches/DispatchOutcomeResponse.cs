namespace hr_sat.Application.Features.Dispatches;

public sealed record DispatchOutcomeResponse(
    long CandidateId,
    string CandidateName,
    string TemplateKind,
    string Status,
    string? Error);
