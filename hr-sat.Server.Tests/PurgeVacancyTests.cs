using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class PurgeVacancyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Purge_vacancy_removes_vacancy_and_owned_requirements()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL", "Dashboard design" }
        });
        createResponse.EnsureSuccessStatusCode();

        var purgeResponse = await client.DeleteAsync(createResponse.Headers.Location);
        var getResponse = await client.GetAsync(createResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.NoContent, purgeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Purge_missing_vacancy_returns_not_found()
    {
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/vacancies/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}