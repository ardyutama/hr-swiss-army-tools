namespace hr_sat.Server.Features.Candidates;

public static class CandidateEndpoints
{
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/vacancies/{id:long}/candidates/import",
                ImportCandidates.HandleAsync)
            .DisableAntiforgery();
        endpoints.MapGet(
            "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/cv-documents/{documentId:long}",
            GetCvDocument.HandleAsync);

        return endpoints;
    }
}