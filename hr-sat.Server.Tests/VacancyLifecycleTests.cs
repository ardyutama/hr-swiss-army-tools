using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class VacancyLifecycleTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    // domain: closed vacancy — retained for reference after its hiring effort ends
    public async Task Close_vacancy_sets_closed_status_and_timestamp()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);

        var closeResponse = await client.PostAsync($"{location}/close", content: null);

        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var persisted = await client.GetFromJsonAsync<VacancyResponse>(location);
        Assert.NotNull(persisted);
        Assert.Equal("closed", persisted.Status);
        Assert.NotNull(persisted.ClosedAt);
    }

    [Fact]
    // domain: closed vacancy is read-only — it cannot be closed again
    public async Task Close_vacancy_rejects_repeated_close()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);
        var firstCloseResponse = await client.PostAsync($"{location}/close", content: null);

        var secondCloseResponse = await client.PostAsync($"{location}/close", content: null);

        Assert.Equal(HttpStatusCode.OK, firstCloseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondCloseResponse.StatusCode);
        var problem = await secondCloseResponse.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("status", problem.Errors.Keys);
    }

    [Fact]
    // domain: closed vacancy can be reopened explicitly
    public async Task Reopen_vacancy_clears_closed_state()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);
        var closeResponse = await client.PostAsync($"{location}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();

        var reopenResponse = await client.PostAsync($"{location}/reopen", content: null);

        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var persisted = await client.GetFromJsonAsync<VacancyResponse>(location);
        Assert.NotNull(persisted);
        Assert.Equal("open", persisted.Status);
        Assert.Null(persisted.ClosedAt);
    }

    [Fact]
    // domain: vacancy status — reopen is valid only for a closed vacancy
    public async Task Reopen_open_vacancy_returns_validation_problem()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);

        var response = await client.PostAsync($"{location}/reopen", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("status", problem.Errors.Keys);
    }

    private static async Task<string> CreateVacancyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        response.EnsureSuccessStatusCode();
        return response.Headers.Location!.OriginalString;
    }

    private sealed record VacancyResponse(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        DateTimeOffset? ClosedAt);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}