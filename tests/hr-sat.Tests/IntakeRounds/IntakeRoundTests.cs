using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests;

public sealed class IntakeRoundTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Creating_a_vacancy_starts_with_default_open_round_one()
    {
        using var client = factory.CreateClient();

        var (_, vacancy) = await CreateVacancyAsync(client);

        var round = Assert.Single(vacancy.Rounds);
        Assert.True(round.Id > 0);
        Assert.Equal(1, round.RoundNumber);
        Assert.Null(round.Name);
        Assert.Equal("open", round.Status);
        Assert.Null(round.ClosedAt);
        Assert.Equal(0, round.CandidateCount);
    }

    [Fact]
    public async Task Creating_a_round_while_another_round_is_open_returns_conflict()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds",
            new { name = "Second wave" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "IntakeRounds.ActiveRoundExists");
    }

    [Fact]
    public async Task Closing_a_round_is_irreversible_and_preserves_candidate_count()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, vacancy) = await CreateVacancyAsync(client);
        var roundId = Assert.Single(vacancy.Rounds).Id;

        using var importResponse = await ImportAsync(
            client,
            vacancyLocation,
            roundId,
            "first@example.com",
            "First application");
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<IntakeRoundResponse>();
        Assert.NotNull(closed);
        Assert.Equal("closed", closed.Status);
        Assert.NotNull(closed.ClosedAt);
        Assert.Equal(1, closed.CandidateCount);

        var persisted = await client.GetFromJsonAsync<VacancyResponse>(vacancyLocation);
        Assert.NotNull(persisted);
        var persistedRound = Assert.Single(persisted.Rounds);
        Assert.Equal("closed", persistedRound.Status);
        Assert.NotNull(persistedRound.ClosedAt);
        Assert.Equal(1, persistedRound.CandidateCount);

        using var repeatedCloseResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.Conflict, repeatedCloseResponse.StatusCode);
        await AssertProblemAsync(repeatedCloseResponse, "IntakeRounds.Closed");
    }

    [Fact]
    public async Task Creating_a_new_round_after_close_scopes_imports_and_lists_to_that_round()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, vacancy) = await CreateVacancyAsync(client);
        var firstRoundId = Assert.Single(vacancy.Rounds).Id;

        using var firstImportResponse = await ImportAsync(
            client,
            vacancyLocation,
            firstRoundId,
            "first@example.com",
            "First application");
        Assert.Equal(HttpStatusCode.OK, firstImportResponse.StatusCode);

        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{firstRoundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        using var createResponse = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds",
            new { name = "  Second wave  " });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var secondRound = await createResponse.Content.ReadFromJsonAsync<IntakeRoundResponse>();
        Assert.NotNull(secondRound);
        Assert.Equal(2, secondRound.RoundNumber);
        Assert.Equal("Second wave", secondRound.Name);
        Assert.Equal("open", secondRound.Status);
        Assert.Equal(0, secondRound.CandidateCount);

        using var secondImportResponse = await ImportAsync(
            client,
            vacancyLocation,
            secondRound.Id,
            "second@example.com",
            "Second application");
        Assert.Equal(HttpStatusCode.OK, secondImportResponse.StatusCode);

        var firstCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{firstRoundId}/candidates");
        var secondCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{secondRound.Id}/candidates");
        Assert.NotNull(firstCandidates);
        Assert.NotNull(secondCandidates);
        Assert.Single(firstCandidates);
        Assert.Single(secondCandidates);
        Assert.Equal("first@example.com", firstCandidates[0].SourceSenderEmail);
        Assert.Equal("second@example.com", secondCandidates[0].SourceSenderEmail);
    }

    [Fact]
    public async Task Writes_and_imports_to_a_closed_round_return_conflict()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, vacancy) = await CreateVacancyAsync(client);
        var roundId = Assert.Single(vacancy.Rounds).Id;

        using var importResponse = await ImportAsync(
            client,
            vacancyLocation,
            roundId,
            "first@example.com",
            "First application");
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var import = await importResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidateId = Assert.Single(import.Results).Candidate!.Id;

        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        using var secondImportResponse = await ImportAsync(
            client,
            vacancyLocation,
            roundId,
            "second@example.com",
            "Second application");
        Assert.Equal(HttpStatusCode.Conflict, secondImportResponse.StatusCode);
        await AssertProblemAsync(secondImportResponse, "IntakeRounds.Closed");

        using var notesResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}/notes",
            new { notes = "Should remain read-only" });
        Assert.Equal(HttpStatusCode.Conflict, notesResponse.StatusCode);
        await AssertProblemAsync(notesResponse, "IntakeRounds.Closed");
    }

    private static async Task<(string Location, VacancyResponse Vacancy)> CreateVacancyAsync(
        HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(vacancy);
        return (location, vacancy);
    }

    private static async Task<HttpResponseMessage> ImportAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string senderEmail,
        string subject)
    {
        using var form = new MultipartFormDataContent();
        var source = CreateEml(senderEmail, subject);
        var fileContent = new ByteArrayContent(source);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", $"{senderEmail}.eml");
        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import",
            form);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        string title)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal(title, problem.Title);
        Assert.Equal((int)response.StatusCode, problem.Status);
    }

    private static byte[] CreateEml(string senderEmail, string subject)
    {
        const string boundary = "hr-sat-round-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var builder = new StringBuilder();
        builder.Append($"From: Applicant <{senderEmail}>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append($"Subject: {subject}\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        builder.Append("Application for the intake round.\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: application/pdf; name=\"candidate.pdf\"\r\n");
        builder.Append("Content-Disposition: attachment; filename=\"candidate.pdf\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        builder.Append(Convert.ToBase64String(pdf));
        builder.Append($"\r\n--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private sealed record VacancyResponse(IReadOnlyList<VacancyRoundResponse> Rounds);

    private sealed record VacancyRoundResponse(
        long Id,
        int RoundNumber,
        string? Name,
        string Status,
        DateTimeOffset? ClosedAt,
        int CandidateCount);

    private sealed record IntakeRoundResponse(
        long Id,
        int RoundNumber,
        string? Name,
        string Status,
        DateTimeOffset? ClosedAt,
        int CandidateCount);

    private sealed record CandidateSummary(
        long Id,
        string? SourceSenderEmail);

    private sealed record ImportResponse(IReadOnlyList<ImportResult> Results);

    private sealed record ImportResult(string Status, ImportedCandidate? Candidate);

    private sealed record ImportedCandidate(long Id);

    private sealed record ProblemResponse(string? Title, int? Status, string? Detail);
}
