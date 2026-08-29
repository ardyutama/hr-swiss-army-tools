using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class ListVacanciesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_vacancies_returns_empty_list_on_fresh_database()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vacancies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vacancies = await response.Content.ReadFromJsonAsync<List<VacancySummary>>();
        Assert.NotNull(vacancies);
        Assert.Empty(vacancies);
    }

    [Fact]
    public async Task Get_vacancies_returns_status_opening_date_and_zero_progress()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        createResponse.EnsureSuccessStatusCode();

        var vacancies = await client.GetFromJsonAsync<List<VacancySummary>>("/api/vacancies");

        Assert.NotNull(vacancies);
        var vacancy = Assert.Single(vacancies);
        Assert.Equal("Data Analyst", vacancy.Title);
        Assert.Equal(new DateOnly(2026, 8, 20), vacancy.OpenedOn);
        Assert.Equal("open", vacancy.Status);
        Assert.Equal(0, vacancy.Progress.ProcessedCandidates);
        Assert.Equal(0, vacancy.Progress.TotalCandidates);
    }

    private sealed record VacancySummary(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        VacancyProgress Progress);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);
}
