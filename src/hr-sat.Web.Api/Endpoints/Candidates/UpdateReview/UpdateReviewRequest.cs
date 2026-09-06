namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed record UpdateReviewRequest(
    string? ReviewStatus,
    string? Notes);
