using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using hr_sat.Application.Features.FormLayouts;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Upserting_form_layout_returns_the_saved_ordinal_bindings() // domain: Form Layout maps CSV columns by ordinal position
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var initialImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            LayoutJson());
        Assert.True(
            initialImport.StatusCode == HttpStatusCode.OK,
            await initialImport.Content.ReadAsStringAsync());
        var definition = new
        {
            columns = new[]
            {
                new { ordinal = 1, role = "name", label = (string?)"Applicant" },
                new { ordinal = 2, role = "contactEmail", label = (string?)"Email address" },
                new { ordinal = 3, role = "contactPhone", label = (string?)null }
            }
        };

        using var putResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/form-layout",
            definition);

        Assert.True(
            putResponse.StatusCode == HttpStatusCode.OK,
            await putResponse.Content.ReadAsStringAsync());
        var saved = await putResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(saved);
        Assert.Equal(new[] { "Timestamp", "Applicant", "Email", "Phone" }, saved.HeaderSnapshot);
        Assert.Collection(
            saved.Columns,
            column =>
            {
                Assert.Equal(1, column.Ordinal);
                Assert.Equal("name", column.Role);
                Assert.Equal("Applicant", column.Label);
            },
            column =>
            {
                Assert.Equal(2, column.Ordinal);
                Assert.Equal("contactemail", column.Role);
                Assert.Equal("Email address", column.Label);
            },
            column =>
            {
                Assert.Equal(3, column.Ordinal);
                Assert.Equal("contactphone", column.Role);
                Assert.Null(column.Label);
            });
        Assert.True(saved.IsValid);

        using var getResponse = await client.GetAsync($"{vacancyLocation}/form-layout");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(saved.Id, fetched.Id);
        Assert.Equal(saved.HeaderSnapshot, fetched.HeaderSnapshot);
        Assert.Equal(saved.Columns, fetched.Columns);
    }

    private static async Task<(string Location, long RoundId)> CreateVacancyAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(vacancy);
        return (response.Headers.Location!.OriginalString, Assert.Single(vacancy.Rounds).Id);
    }

    private static async Task<HttpResponseMessage> ImportFormAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string layout)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(
            "Timestamp,Applicant,Email,Phone\r\n\"2026-09-16T10:00:00Z\",\"Alice\",\"alice@example.com\",\"123\"\r\n"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "responses.csv");
        form.Add(new StringContent(layout), "layout");
        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import-form",
            form);
    }

    private static string LayoutJson() => JsonSerializer.Serialize(new
    {
        columns = new[]
        {
            new { ordinal = 1, role = "name", label = (string?)null },
            new { ordinal = 2, role = "contactEmail", label = (string?)null },
            new { ordinal = 3, role = "contactPhone", label = (string?)null }
        }
    });

    private sealed record VacancyResponse(IReadOnlyList<RoundResponse> Rounds);

    private sealed record RoundResponse(long Id);
}
