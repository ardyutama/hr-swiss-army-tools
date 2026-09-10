using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class PromoteCandidatesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Promote_candidates_should_move_the_selection_and_preserve_review_data() // domain: Promote preserves candidate review data
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(
            client,
            "SQL",
            "VAT reporting");
        var vacancy = await client.GetFromJsonAsync<VacancyDetails>(vacancyLocation);
        Assert.NotNull(vacancy);
        var candidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "Alice Applicant");
        await UpdateReviewAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            candidate.Id,
            "flagged",
            "Keep this note.");

        var requirementResponse = await client.PutAsJsonAsync(
            CandidatePath(
                vacancyLocation,
                sourceRoundId,
                candidate.Id,
                $"requirement-reviews/{vacancy.Requirements[0].Id}"),
            new { confirmed = true });
        Assert.Equal(HttpStatusCode.OK, requirementResponse.StatusCode);

        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");

        var promotionResponse = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{activeRoundId}/promotions",
            new
            {
                sourceRoundId,
                candidateIds = new[] { candidate.Id }
            });

        Assert.Equal(HttpStatusCode.OK, promotionResponse.StatusCode);
        var summaries = await promotionResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(summaries);
        var summary = Assert.Single(summaries);
        Assert.Equal(candidate.Id, summary.Id);
        Assert.Equal("flagged", summary.ReviewStatus);
        Assert.Equal("Keep this note.", summary.Notes);
        Assert.Equal(1, summary.CvDocumentCount);

        var details = await client.GetFromJsonAsync<CandidateDetails>(
            CandidatePath(vacancyLocation, activeRoundId, candidate.Id));
        Assert.NotNull(details);
        Assert.Equal("flagged", details.ReviewStatus);
        Assert.Equal("none", details.HireOutcome);
        Assert.Equal("Keep this note.", details.Notes);
        Assert.Equal(1, details.PromotedFromRoundNumber);
        Assert.NotNull(details.PromotedAt);
        Assert.Contains(
            details.RequirementReviews,
            review => review.RequirementId == vacancy.Requirements[0].Id && review.Confirmed);

        var sourceCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{sourceRoundId}/candidates");
        Assert.NotNull(sourceCandidates);
        Assert.Empty(sourceCandidates);
    }

    [Fact]
    public async Task Promotion_should_reject_a_mixed_selection_without_moving_any_candidate() // domain: Promote is atomic
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(client, "SQL");
        var flaggedCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "Alice Applicant");
        var rejectedCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "Bob Applicant");
        await UpdateReviewAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            rejectedCandidate.Id,
            "rejected",
            "Do not promote.");

        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{activeRoundId}/promotions",
            new
            {
                sourceRoundId,
                candidateIds = new[] { flaggedCandidate.Id, rejectedCandidate.Id }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("candidateIds", problem.Errors.Keys);
        Assert.Contains(
            problem.Errors["candidateIds"],
            message => message.Contains(rejectedCandidate.Id.ToString()));

        var sourceCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{sourceRoundId}/candidates");
        var activeCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{activeRoundId}/candidates");
        Assert.NotNull(sourceCandidates);
        Assert.NotNull(activeCandidates);
        Assert.Equal(2, sourceCandidates.Count);
        Assert.Empty(activeCandidates);
    }

    [Fact]
    public async Task Promotion_should_reject_a_closed_vacancy_with_conflict() // domain: Closed Vacancy
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, "SQL");
        var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/promotions",
            new { sourceRoundId = 999L, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Vacancies.Closed", problem.Title);
    }

    [Fact]
    public async Task Promotion_should_report_no_active_round_when_the_only_round_is_closed() // domain: Active Round
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, "SQL");
        await CloseRoundAsync(client, vacancyLocation, roundId);

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/promotions",
            new { sourceRoundId = 999L, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("IntakeRounds.NoActiveRound", problem.Title);
    }

    [Fact]
    public async Task Promotion_should_reject_a_closed_route_round_when_another_round_is_active() // domain: Round Closure
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(client, "SQL");
        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{sourceRoundId}/promotions",
            new { sourceRoundId = 999L, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("IntakeRounds.Closed", problem.Title);
        Assert.True(activeRoundId > 0);
    }

    [Fact]
    public async Task Promotion_should_report_an_unknown_route_round() // domain: Intake Round
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(client, "SQL");
        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/999999/promotions",
            new { sourceRoundId, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("IntakeRounds.NotFound", problem.Title);
        Assert.True(activeRoundId > 0);
    }

    [Fact]
    public async Task Promotion_should_report_an_unknown_source_round() // domain: Intake Round
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(client, "SQL");
        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{activeRoundId}/promotions",
            new { sourceRoundId = 999999L, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("IntakeRounds.NotFound", problem.Title);
    }

    [Fact]
    public async Task Promotion_should_reject_the_active_round_as_source() // domain: Promote
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, "SQL");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/promotions",
            new { sourceRoundId = roundId, candidateIds = new[] { 1L } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("sourceRoundId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Promotion_should_report_conflict_when_source_email_already_exists_in_active_round() // domain: one source email per round
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateVacancyAsync(client, "SQL");
        var sourceCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "Duplicate Applicant");
        await CloseRoundAsync(client, vacancyLocation, sourceRoundId);
        var activeRoundId = await CreateRoundAsync(client, vacancyLocation, "Second wave");
        var activeCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            activeRoundId,
            "Duplicate Applicant");

        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{activeRoundId}/promotions",
            new
            {
                sourceRoundId,
                candidateIds = new[] { sourceCandidate.Id }
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Candidates.SourceEmailAlreadyInRound", problem.Title);

        var sourceCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{sourceRoundId}/candidates");
        var activeCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{activeRoundId}/candidates");
        Assert.NotNull(sourceCandidates);
        Assert.NotNull(activeCandidates);
        Assert.Equal(sourceCandidate.Id, Assert.Single(sourceCandidates).Id);
        Assert.Equal(activeCandidate.Id, Assert.Single(activeCandidates).Id);
    }

    private static async Task<(string Location, long RoundId)> CreateVacancyAsync(
        HttpClient client,
        params string[] requirements)
    {
        var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements
        });
        response.EnsureSuccessStatusCode();
        var location = response.Headers.Location!.OriginalString;
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyDetails>();
        Assert.NotNull(vacancy);
        return (location, Assert.Single(vacancy.Rounds).Id);
    }

    private static async Task<long> CreateRoundAsync(
        HttpClient client,
        string vacancyLocation,
        string name)
    {
        var response = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds",
            new { name });
        response.EnsureSuccessStatusCode();
        var round = await response.Content.ReadFromJsonAsync<RoundDetails>();
        Assert.NotNull(round);
        return round.Id;
    }

    private static async Task CloseRoundAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId)
    {
        var response = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<ImportedCandidate> ImportCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string senderName)
    {
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                senderName,
                $"{senderName.Replace(' ', '.').ToLowerInvariant()}@example.com",
                $"{senderName} application",
                "Please review my application.",
                ("candidate.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF"))),
            "candidate.eml");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import",
            form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var outcome = Assert.Single(import.Results);
        Assert.Equal("imported", outcome.Status);
        Assert.NotNull(outcome.Candidate);
        return outcome.Candidate;
    }

    private static async Task UpdateReviewAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId,
        string reviewStatus,
        string notes)
    {
        var response = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, roundId, candidateId, "review"),
            new { reviewStatus, notes });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CandidatePath(
        string vacancyLocation,
        long roundId,
        long candidateId,
        string? suffix = null) =>
        $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}{(suffix is null ? string.Empty : $"/{suffix}")}";

    private static void AddFile(MultipartFormDataContent form, byte[] content, string filename)
    {
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", filename);
    }

    private static byte[] CreateEml(
        string senderName,
        string senderEmail,
        string subject,
        string body,
        params (string Filename, byte[] Content)[] attachments)
    {
        const string boundary = "hr-sat-boundary";
        var builder = new StringBuilder();
        builder.Append($"From: {senderName} <{senderEmail}>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append($"Subject: {subject}\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n");
        builder.Append("\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        builder.Append(body);
        builder.Append("\r\n");

        foreach (var attachment in attachments)
        {
            builder.Append($"--{boundary}\r\n");
            builder.Append($"Content-Type: application/pdf; name=\"{attachment.Filename}\"\r\n");
            builder.Append($"Content-Disposition: attachment; filename=\"{attachment.Filename}\"\r\n");
            builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
            builder.Append(Convert.ToBase64String(attachment.Content));
            builder.Append("\r\n");
        }

        builder.Append($"--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private sealed record VacancyDetails(
        IReadOnlyList<RequirementDetails> Requirements,
        IReadOnlyList<RoundDetails> Rounds);

    private sealed record RequirementDetails(long Id, string Phrase);

    private sealed record RoundDetails(long Id, int RoundNumber, string Status);

    private sealed record ImportedCandidate(long Id);

    private sealed record ImportResponse(IReadOnlyList<ImportFileResponse> Results);

    private sealed record ImportFileResponse(string Status, ImportedCandidate? Candidate);

    private sealed record CandidateSummary(
        long Id,
        string? FullName,
        string? ContactEmail,
        string? Notes,
        string ReviewStatus,
        string HireOutcome,
        string? SourceSenderName,
        string? SourceSenderEmail,
        string? SourceSubject,
        DateTimeOffset? SourceSentAt,
        int CvDocumentCount);

    private sealed record CandidateDetails(
        long Id,
        string ReviewStatus,
        string HireOutcome,
        int? PromotedFromRoundNumber,
        DateTimeOffset? PromotedAt,
        string? FullName,
        string? ContactEmail,
        string? Notes,
        IReadOnlyList<RequirementReview> RequirementReviews);

    private sealed record RequirementReview(long RequirementId, bool Confirmed);

    private sealed record ProblemResponse(string? Title);

    private sealed record ValidationProblemResponse(
        IReadOnlyDictionary<string, string[]> Errors);
}