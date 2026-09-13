using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Tests;

public sealed class ListVacanciesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Get_vacancies_returns_status_opening_date_and_zero_progress() // US-10: HR sees all vacancies with status and progress
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
        Assert.Null(vacancy.Hiring);
    }

    [Fact]
    public async Task Get_vacancies_projects_needed_hires_with_zero_active_hires()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" },
            neededHires = 10
        });
        createResponse.EnsureSuccessStatusCode();

        var vacancies = await client.GetFromJsonAsync<List<VacancySummary>>("/api/vacancies");

        Assert.NotNull(vacancies);
        var vacancy = Assert.Single(vacancies);
        Assert.NotNull(vacancy.Hiring);
        Assert.Equal(10, vacancy.Hiring.NeededHires);
        Assert.Equal(0, vacancy.Hiring.ActiveHires);
    }

    private sealed record VacancySummary(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        VacancyProgress Progress,
        VacancyHiring? Hiring);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);

    private sealed record VacancyHiring(int NeededHires, int ActiveHires);
}
