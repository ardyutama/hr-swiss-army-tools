namespace hr_sat.Server.Features.Vacancies;

public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vacancies").WithTags("Vacancies");
        group.MapGet("", ListVacancies.HandleAsync).WithName("ListVacancies");
        group.MapGet("/{id:long}", GetVacancy.HandleAsync).WithName("GetVacancy");
        group.MapPost("", CreateVacancy.HandleAsync).WithName("CreateVacancy");
        group.MapPut("/{id:long}", UpdateVacancy.HandleAsync).WithName("UpdateVacancy");
        group.MapPost("/{id:long}/close", CloseVacancy.HandleAsync).WithName("CloseVacancy");
        group.MapPost("/{id:long}/reopen", ReopenVacancy.HandleAsync).WithName("ReopenVacancy");
        group.MapDelete("/{id:long}", PurgeVacancy.HandleAsync).WithName("PurgeVacancy");

        return endpoints;
    }
}