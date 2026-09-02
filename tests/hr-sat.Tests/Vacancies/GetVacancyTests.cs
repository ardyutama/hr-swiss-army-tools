using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Tests;

public sealed class GetVacancyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_vacancy_returns_requirements_in_position_order()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL", "Dashboard design" }
        });
        createResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync(createResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(vacancy);
        Assert.Equal("Data Analyst", vacancy.Title);
        Assert.Equal(
            new[] { "SQL", "Dashboard design" },
            vacancy.Requirements.Select(requirement => requirement.Phrase));
        Assert.Equal(new[] { 1, 2 }, vacancy.Requirements.Select(requirement => requirement.Position));
    }

    private sealed record VacancyResponse(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        IReadOnlyList<VacancyRequirementResponse> Requirements);

    private sealed record VacancyRequirementResponse(string Phrase, int Position);
}