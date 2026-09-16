using System.Net;
using System.Net.Http.Json;
using hr_sat.Application.Features.FormLayouts;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Upserting_form_layout_returns_the_saved_ordinal_bindings() // domain: Form Layout maps CSV columns by ordinal position
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var definition = new
        {
            headerSnapshot = new[] { "Timestamp", "Applicant", "Email", "Phone" },
            nameColumnOrdinal = 1,
            contactEmailColumnOrdinal = 2,
            contactPhoneColumnOrdinal = 3
        };

        using var putResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/form-layout",
            definition);

        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
        var saved = await putResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(saved);
        Assert.Equal(new[] { "Timestamp", "Applicant", "Email", "Phone" }, saved.HeaderSnapshot);
        Assert.Equal(1, saved.NameColumnOrdinal);
        Assert.Equal(2, saved.ContactEmailColumnOrdinal);
        Assert.Equal(3, saved.ContactPhoneColumnOrdinal);
        Assert.True(saved.IsValid);

        using var getResponse = await client.GetAsync($"{vacancyLocation}/form-layout");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(saved.Id, fetched.Id);
        Assert.Equal(saved.HeaderSnapshot, fetched.HeaderSnapshot);
    }

    private static async Task<string> CreateVacancyAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response.Headers.Location!.OriginalString;
    }
}
