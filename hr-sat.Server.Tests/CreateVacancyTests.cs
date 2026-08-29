using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class CreateVacancyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Create_vacancy_preserves_requirement_order()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            openedOn = "2026-08-29",
            requirements = new[] { "Forklift certification", "Inventory control" }
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(vacancy);
        Assert.True(vacancy.Id > 0);
        Assert.Equal("Warehouse Coordinator", vacancy.Title);
        Assert.Equal(new DateOnly(2026, 8, 29), vacancy.OpenedOn);
        Assert.Equal("open", vacancy.Status);
        Assert.Equal(
            new[] { "Forklift certification", "Inventory control" },
            vacancy.Requirements.Select(requirement => requirement.Phrase));
        Assert.Equal(new[] { 1, 2 }, vacancy.Requirements.Select(requirement => requirement.Position));
        Assert.Equal($"/api/vacancies/{vacancy.Id}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Create_vacancy_requires_at_least_one_requirement()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            openedOn = "2026-08-29",
            requirements = Array.Empty<string>()
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("requirements", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_vacancy_rejects_duplicate_normalized_requirements()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            openedOn = "2026-08-29",
            requirements = new[] { " Forklift certification ", "forklift certification" }
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("requirements", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_vacancy_requires_a_nonblank_title()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "   ",
            openedOn = "2026-08-29",
            requirements = new[] { "Inventory control" }
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("title", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_vacancy_requires_an_opening_date()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            requirements = new[] { "Inventory control" }
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("openedOn", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_vacancy_requires_nonblank_requirement_phrases()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            openedOn = "2026-08-29",
            requirements = new[] { "   " }
        };

        var response = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("requirements", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_vacancy_allows_repeated_title_and_opening_date()
    {
        using var client = factory.CreateClient();
        var request = new
        {
            title = "Warehouse Coordinator",
            openedOn = "2026-08-29",
            requirements = new[] { "Inventory control" }
        };

        var firstResponse = await client.PostAsJsonAsync("/api/vacancies", request);
        var secondResponse = await client.PostAsJsonAsync("/api/vacancies", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<VacancyResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Id, second.Id);
    }

    private sealed record VacancyResponse(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        IReadOnlyList<VacancyRequirementResponse> Requirements);

    private sealed record VacancyRequirementResponse(string Phrase, int Position);

    private sealed record ValidationProblemResponse(
        Dictionary<string, string[]> Errors);
}