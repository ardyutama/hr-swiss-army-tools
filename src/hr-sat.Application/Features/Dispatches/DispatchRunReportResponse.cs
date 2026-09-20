namespace hr_sat.Application.Features.Dispatches;

public sealed record DispatchRunReportResponse(
    long? RunId,
    int SentCount,
    int FailedCount,
    int ExcludedCount,
    IReadOnlyList<DispatchOutcomeResponse> Outcomes,
    IReadOnlyList<DispatchExclusionResponse> Excluded);
