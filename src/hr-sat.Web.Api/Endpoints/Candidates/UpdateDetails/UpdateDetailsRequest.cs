namespace hr_sat.Web.Api.Endpoints.Candidates;

internal sealed record UpdateDetailsRequest(
    string? FullName,
    string? ContactEmail);
