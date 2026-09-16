namespace hr_sat.Application.Features.Candidates.ImportForm;

public sealed record ImportFormResponse(
    int RowsRead,
    int Created,
    int Updated,
    int SkippedOutdated,
    int PriorApplications);