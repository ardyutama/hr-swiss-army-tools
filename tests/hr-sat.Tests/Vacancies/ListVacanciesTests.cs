using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        Assert.Equal(0, vacancy.ReviewCounts.New);
        Assert.Equal(0, vacancy.ReviewCounts.Flagged);
        Assert.Equal(0, vacancy.ReviewCounts.Shortlisted);
        Assert.Equal(0, vacancy.ReviewCounts.Rejected);
        Assert.Null(vacancy.Hiring);
    }

    [Fact]
    public async Task Get_vacancies_projects_review_counts_per_status() // US-10: progress reflects the review pipeline per vacancy
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        createResponse.EnsureSuccessStatusCode();
        var location = createResponse.Headers.Location!.OriginalString;
        var vacancy = await createResponse.Content.ReadFromJsonAsync<VacancyDetails>();
        Assert.NotNull(vacancy);
        var roundId = Assert.Single(vacancy.Rounds).Id;

        var candidates = await ImportCandidatesAsync(client, location, roundId, "Alice", "Bianca", "Carlos", "Diana");
        await UpdateReviewAsync(client, location, roundId, candidates[1].Id, "flagged");
        await UpdateReviewAsync(client, location, roundId, candidates[2].Id, "shortlisted");
        await UpdateReviewAsync(client, location, roundId, candidates[3].Id, "rejected");

        var vacancies = await client.GetFromJsonAsync<List<VacancySummary>>("/api/vacancies");

        Assert.NotNull(vacancies);
        var summary = Assert.Single(vacancies);
        Assert.Equal(1, summary.ReviewCounts.New);
        Assert.Equal(1, summary.ReviewCounts.Flagged);
        Assert.Equal(1, summary.ReviewCounts.Shortlisted);
        Assert.Equal(1, summary.ReviewCounts.Rejected);
        Assert.Equal(2, summary.Progress.ProcessedCandidates);
        Assert.Equal(4, summary.Progress.TotalCandidates);
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

    private static async Task<IReadOnlyList<ImportedCandidate>> ImportCandidatesAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        params string[] senderNames)
    {
        using var form = new MultipartFormDataContent();
        for (var index = 0; index < senderNames.Length; index++)
        {
            var senderName = senderNames[index];
            var eml = $"""
                From: {senderName} <candidate{index + 1}@example.com>
                To: hr@example.com
                Date: Sat, 29 Aug 2026 10:00:00 +0000
                Subject: {senderName} application

                Please review my application.
                """.Replace("\n", "\r\n");
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(eml));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
            form.Add(fileContent, "files", $"candidate-{index + 1}.eml");
        }

        var response = await client.PostAsync($"{vacancyLocation}/rounds/{roundId}/candidates/import", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.All(import.Results, result => Assert.Equal("imported", result.Status));
        return import.Results.Select(result => result.Candidate!).ToList();
    }

    private static async Task UpdateReviewAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId,
        string reviewStatus)
    {
        var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}/review",
            new { reviewStatus, notes = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record VacancySummary(
        long Id,
        string Title,
        DateOnly OpenedOn,
        string Status,
        VacancyProgress Progress,
        VacancyReviewCounts ReviewCounts,
        VacancyHiring? Hiring);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);

    private sealed record VacancyReviewCounts(int New, int Flagged, int Shortlisted, int Rejected);

    private sealed record VacancyHiring(int NeededHires, int ActiveHires);

    private sealed record VacancyDetails(IReadOnlyList<VacancyRound> Rounds);

    private sealed record VacancyRound(long Id);

    private sealed record ImportResponse(IReadOnlyList<ImportFileResponse> Results);

    private sealed record ImportFileResponse(string Status, ImportedCandidate? Candidate);

    private sealed record ImportedCandidate(long Id);
}
