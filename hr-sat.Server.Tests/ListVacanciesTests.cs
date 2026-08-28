using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace hr_swiss_army_tools.Server.Tests;

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
}
