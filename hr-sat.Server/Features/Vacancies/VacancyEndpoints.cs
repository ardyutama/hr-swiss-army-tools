namespace hr_sat.Server.Features.Vacancies;

public static class VacancyEndpoints
{
    public static IEndpointRouteBuilder MapVacancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vacancies");
        group.MapGet("", ListVacancies.HandleAsync);
        group.MapGet("/{id:long}", GetVacancy.HandleAsync);
        group.MapPost("", CreateVacancy.HandleAsync);
        group.MapPut("/{id:long}", UpdateVacancy.HandleAsync);
        group.MapPost("/{id:long}/close", CloseVacancy.HandleAsync);
        group.MapPost("/{id:long}/reopen", ReopenVacancy.HandleAsync);
        group.MapDelete("/{id:long}", PurgeVacancy.HandleAsync);

        return endpoints;
    }
}