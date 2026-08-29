using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class UpdateVacancyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Update_vacancy_replaces_definition_and_preserves_new_requirement_order()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL", "Dashboard design" }
        });
        createResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync(createResponse.Headers.Location, new
        {
            title = "Senior Data Analyst",
            openedOn = "2026-08-21",
            requirements = new[] { "Stakeholder communication", "SQL" }
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var persisted = await client.GetFromJsonAsync<VacancyResponse>(createResponse.Headers.Location);
        Assert.NotNull(persisted);
        Assert.Equal("Senior Data Analyst", persisted.Title);
        Assert.Equal(new DateOnly(2026, 8, 21), persisted.OpenedOn);
        Assert.Equal(
            new[] { "Stakeholder communication", "SQL" },
            persisted.Requirements.Select(requirement => requirement.Phrase));
        Assert.Equal(new[] { 1, 2 }, persisted.Requirements.Select(requirement => requirement.Position));
    }

    [Fact]
    public async Task Update_closed_vacancy_is_rejected()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);
        var closeResponse = await client.PostAsync($"{location}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync(location, new
        {
            title = "Senior Data Analyst",
            openedOn = "2026-08-21",
            requirements = new[] { "Stakeholder communication" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var problem = await updateResponse.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("status", problem.Errors.Keys);
        var persisted = await client.GetFromJsonAsync<VacancyResponse>(location);
        Assert.NotNull(persisted);
        Assert.Equal("Data Analyst", persisted.Title);
        Assert.Equal(new DateOnly(2026, 8, 20), persisted.OpenedOn);
    }

    [Fact]
    public async Task Reopened_vacancy_can_be_updated()
    {
        using var client = factory.CreateClient();
        var location = await CreateVacancyAsync(client);
        var closeResponse = await client.PostAsync($"{location}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();
        var reopenResponse = await client.PostAsync($"{location}/reopen", content: null);
        reopenResponse.EnsureSuccessStatusCode();

        var updateResponse = await client.PutAsJsonAsync(location, new
        {
            title = "Senior Data Analyst",
            openedOn = "2026-08-21",
            requirements = new[] { "Stakeholder communication" }
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var persisted = await client.GetFromJsonAsync<VacancyResponse>(location);
        Assert.NotNull(persisted);
        Assert.Equal("Senior Data Analyst", persisted.Title);
        Assert.Equal("open", persisted.Status);
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
        IReadOnlyList<VacancyRequirementResponse> Requirements);

    private sealed record VacancyRequirementResponse(string Phrase, int Position);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}