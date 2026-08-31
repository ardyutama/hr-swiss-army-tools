using hr_sat.Server.Features.Candidates.Import;

namespace hr_sat.Server.Features.Candidates;

public static class CandidateEndpoints
{
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/vacancies/{vacancyId:long}/candidates/import",
                ImportCandidates.HandleAsync)
            .WithTags("Candidates")
            .WithName("ImportCandidates")
            .DisableAntiforgery();
        endpoints.MapGet(
                "/api/vacancies/{vacancyId:long}/candidates",
                ListCandidates.HandleAsync)
            .WithTags("Candidates")
            .WithName("ListCandidates");
        endpoints.MapGet(
                "/api/vacancies/{vacancyId:long}/candidates/{candidateId:long}/cv-documents/{documentId:long}",
                GetCvDocument.HandleAsync)
            .WithTags("Candidates")
            .WithName("GetCvDocument");

        return endpoints;
    }
}