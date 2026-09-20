using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace hr_sat.Tests.ScreeningRules;

public sealed class ScreeningRuleIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Screening_preview_and_list_use_form_rows_only_and_return_rule_chips()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var formImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(
                CsvRow("2026-09-16T10:00:00Z", "Alice", "alice@example.com", " No "),
                CsvRow("2026-09-16T11:00:00Z", "Bob", "bob@example.com", "Yes")));
        Assert.Equal(HttpStatusCode.OK, formImport.StatusCode);
        using var emailImport = await ImportEmailAsync(
            client,
            vacancyLocation,
            roundId,
            "email@example.com");
        Assert.Equal(HttpStatusCode.OK, emailImport.StatusCode);

        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        using var previewResponse = await client.PostAsJsonAsync(
            ScreeningRulesPath(vacancyLocation, "preview"),
            new { rules = new[] { Rule(3, "equals", "no") } });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(2, preview.Total);
        Assert.Equal(1, preview.ScreenedOut);
        Assert.Equal(1, Assert.Single(preview.PerRule).ScreenedOut);

        var defaultPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId);
        Assert.Equal(2, defaultPage.Total);
        Assert.Equal(2, defaultPage.FilteredTotal);
        Assert.Equal(2, defaultPage.Items.Count);
        Assert.Equal(2, defaultPage.Counts.Outcome.Any);
        Assert.Equal(1, defaultPage.Counts.ScreenedOut);
        Assert.DoesNotContain(defaultPage.Items, candidate => candidate.ScreenedOut);
        Assert.Contains(defaultPage.Items, candidate => candidate.ContactEmail == "bob@example.com");
        Assert.Contains(defaultPage.Items, candidate => candidate.IntakeSource == "email");

        var allPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        Assert.Equal(3, allPage.Total);
        Assert.Equal(3, allPage.FilteredTotal);
        var screened = Assert.Single(allPage.Items, candidate => candidate.ScreenedOut);
        Assert.Equal("new", screened.ReviewStatus);
        var firedRule = Assert.Single(screened.FiredRules);
        Assert.Equal(0, firedRule.Index);
        Assert.Equal("Availability · equals \"no\"", firedRule.Display);

        var details = await client.GetFromJsonAsync<CandidateDetailsResponse>(
            CandidatePath(vacancyLocation, roundId, screened.Id));
        Assert.NotNull(details);
        Assert.NotNull(details.Screening);
        Assert.True(details.Screening.ScreenedOut);
        Assert.Equal(firedRule.Display, Assert.Single(details.Screening.FiredRules).Display);
    }

    [Fact]
    public async Task Resubmission_screening_uses_only_the_current_form_response()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var firstImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(CsvRow("2026-09-16T10:00:00Z", "Applicant", "person@example.com", "No")));
        Assert.Equal(HttpStatusCode.OK, firstImport.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        using var secondImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(CsvRow("2026-09-16T11:00:00Z", "Applicant", "PERSON@example.com", "Yes")));
        Assert.Equal(HttpStatusCode.OK, secondImport.StatusCode);

        var page = await GetPageAsync(client, vacancyLocation, roundId);
        var candidate = Assert.Single(page.Items);
        Assert.False(candidate.ScreenedOut);
        Assert.True(candidate.IsResubmitted);

        var details = await client.GetFromJsonAsync<CandidateDetailsResponse>(
            CandidatePath(vacancyLocation, roundId, candidate.Id));
        Assert.NotNull(details);
        Assert.False(details.Screening!.ScreenedOut);
        Assert.Equal(2, details.FormResponses.Count);
        Assert.Equal(
            "Yes",
            Assert.Single(details.FormResponses, response => response.IsCurrent).Cells[3]);
    }

    [Fact]
    public async Task Closing_a_round_freezes_screening_before_later_rule_edits()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(
                CsvRow("2026-09-16T10:00:00Z", "Alice", "alice@example.com", "No"),
                CsvRow("2026-09-16T11:00:00Z", "Bob", "bob@example.com", "Yes")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "yes"));

        var closedPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        var screened = Assert.Single(closedPage.Items, candidate => candidate.ContactEmail == "alice@example.com");
        Assert.True(screened.ScreenedOut);
        Assert.Equal("Availability · equals \"no\"", Assert.Single(screened.FiredRules).Display);
        var visible = Assert.Single(
            closedPage.Items,
            candidate => candidate.ContactEmail == "bob@example.com");
        Assert.False(visible.ScreenedOut);

        using var previewResponse = await client.PostAsJsonAsync(
            ScreeningRulesPath(vacancyLocation, "preview"),
            new { rules = new[] { Rule(3, "equals", "yes") } });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(0, preview.Total);
        Assert.Equal(0, preview.ScreenedOut);
        Assert.Equal(0, Assert.Single(preview.PerRule).ScreenedOut);
    }

    [Fact]
    public async Task Active_rule_fix_resurfaces_a_candidate_and_allows_removal()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(CsvRow("2026-09-16T10:00:00Z", "Applicant", "person@example.com", "No")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        var screened = await GetSingleCandidateAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        using var blockedDelete = await client.DeleteAsync(
            CandidatePath(vacancyLocation, roundId, screened.Id));
        Assert.Equal(HttpStatusCode.Conflict, blockedDelete.StatusCode);
        var blockedProblem = await blockedDelete.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(blockedProblem);
        Assert.Equal("Candidates.ScreenedOut", blockedProblem.Title);
        Assert.Contains("Availability", blockedProblem.Detail);

        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "yes"));

        var resurfaced = await GetPageAsync(client, vacancyLocation, roundId);
        Assert.Single(resurfaced.Items);
        Assert.False(resurfaced.Items[0].ScreenedOut);
        using var deleteResponse = await client.DeleteAsync(
            CandidatePath(vacancyLocation, roundId, screened.Id));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Promotion_drops_the_frozen_verdict_and_re_evaluates_in_the_active_round()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, sourceRoundId) = await CreateConfiguredVacancyAsync(client);

        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            Csv(CsvRow("2026-09-16T10:00:00Z", "Applicant", "person@example.com", "No")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));
        var sourceCandidate = await GetSingleCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "?screened=all");

        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{sourceRoundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        using var createRoundResponse = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds",
            new { name = "Second wave" });
        Assert.Equal(HttpStatusCode.OK, createRoundResponse.StatusCode);
        var activeRound = await createRoundResponse.Content.ReadFromJsonAsync<RoundResponse>();
        Assert.NotNull(activeRound);

        var frozenSummary = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{sourceRoundId}/promote-summary");
        Assert.NotNull(frozenSummary);
        var frozen = Assert.Single(frozenSummary);
        Assert.True(frozen.ScreenedOut);
        Assert.Single(frozen.FiredRules);

        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "yes"));
        var stillFrozen = await GetSingleCandidateAsync(
            client,
            vacancyLocation,
            sourceRoundId,
            "?screened=all");
        Assert.True(stillFrozen.ScreenedOut);

        using var promoteResponse = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds/{activeRound.Id}/promotions",
            new
            {
                sourceRoundId,
                candidateIds = new[] { sourceCandidate.Id }
            });
        Assert.Equal(HttpStatusCode.OK, promoteResponse.StatusCode);
        var promoted = await promoteResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(promoted);
        var promotedCandidate = Assert.Single(promoted);
        Assert.False(promotedCandidate.ScreenedOut);
        Assert.Empty(promotedCandidate.FiredRules);

        var activePage = await GetPageAsync(client, vacancyLocation, activeRound.Id);
        Assert.Single(activePage.Items);
        Assert.False(activePage.Items[0].ScreenedOut);
    }

    [Fact]
    public async Task Screening_rule_validation_rejects_invalid_rule_definitions()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateConfiguredVacancyAsync(client);

        using var tooManyResponse = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new
            {
                rules = Enumerable.Range(0, 6)
                    .Select(index => Rule(3, "equals", $"value-{index}"))
                    .ToArray()
            });
        await AssertValidationProblemAsync(tooManyResponse);

        using var missingValueResponse = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new { rules = new[] { Rule(3, "equals", "   ") } });
        await AssertValidationProblemAsync(missingValueResponse);

        using var unexpectedValueResponse = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new { rules = new[] { Rule(3, "is-empty", "unexpected") } });
        await AssertValidationProblemAsync(unexpectedValueResponse);

        using var negativeOrdinalResponse = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new { rules = new[] { Rule(-1, "equals", "value") } });
        await AssertValidationProblemAsync(negativeOrdinalResponse);

        using var unknownOrdinalResponse = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new { rules = new[] { Rule(4, "equals", "value") } });
        await AssertValidationProblemAsync(unknownOrdinalResponse);
    }

    [Fact]
    public async Task Screening_preview_validation_rejects_invalid_query_input()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/vacancies/0/screening-rules/preview",
            new { rules = Array.Empty<object>() });

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task Paged_list_excludes_screened_rows_and_keeps_counts_independent_of_filters()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);
        // Identical timestamps pin the id tiebreak: the list orders by the coalesced
        // received moment (form Timestamp), so spread timestamps would scatter pages.
        var rows = Enumerable.Range(1, 101)
            .Select(index => CsvRow(
                "2026-09-16T10:00:00Z",
                $"Candidate {index}",
                $"candidate{index}@example.com",
                index == 101 ? "No" : "Yes"))
            .ToArray();
        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(rows));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        var allFirstPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        var allSecondPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all&page=2");
        Assert.Equal(1, allFirstPage.Page);
        Assert.Equal(100, allFirstPage.PageSize);
        Assert.Equal(101, allFirstPage.Total);
        Assert.Equal(100, allFirstPage.Items.Count);
        Assert.Equal(101, allSecondPage.Total);
        var screened = Assert.Single(allSecondPage.Items);
        Assert.True(screened.ScreenedOut);

        var defaultSecondPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?page=2");
        Assert.Equal(100, defaultSecondPage.Total);
        Assert.Empty(defaultSecondPage.Items);
        Assert.Equal(1, defaultSecondPage.Counts.ScreenedOut);

        using var reviewResponse = await client.PutAsJsonAsync(
            $"{CandidatePath(vacancyLocation, roundId, allFirstPage.Items[0].Id)}/review",
            new { reviewStatus = "flagged", notes = "Needs a closer look." });
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        var filteredPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?status=flagged&query=Candidate");
        Assert.Single(filteredPage.Items);
        Assert.Equal(1, filteredPage.FilteredTotal);
        Assert.Equal(99, filteredPage.Counts.Status.New);
        Assert.Equal(1, filteredPage.Counts.Status.Flagged);
        Assert.Equal(100, filteredPage.Counts.Outcome.Any);
        Assert.Equal(1, filteredPage.Counts.ScreenedOut);
    }

    [Fact]
    public async Task Review_queue_and_vacancy_progress_exclude_screened_out_candidates()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(
                CsvRow("2026-09-16T10:00:00Z", "Screened", "screened@example.com", "No"),
                CsvRow("2026-09-16T11:00:00Z", "Shortlisted", "shortlisted@example.com", "Yes"),
                CsvRow("2026-09-16T12:00:00Z", "New", "new@example.com", "Yes")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));
        var allPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        var shortlisted = Assert.Single(
            allPage.Items,
            candidate => candidate.ContactEmail == "shortlisted@example.com");
        await UpdateReviewAsync(
            client,
            vacancyLocation,
            roundId,
            shortlisted.Id,
            "shortlisted");

        var reviewQueue = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/review-queue");
        Assert.NotNull(reviewQueue);
        Assert.Equal(2, reviewQueue.Count);
        Assert.DoesNotContain(reviewQueue, candidate => candidate.ScreenedOut);
        Assert.Contains(reviewQueue, candidate => candidate.ContactEmail == "shortlisted@example.com");

        var vacancy = await client.GetFromJsonAsync<VacancyDetails>(vacancyLocation);
        Assert.NotNull(vacancy);
        Assert.Equal(1, vacancy.Progress.ProcessedCandidates);
        Assert.Equal(2, vacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Review_queue_mirrors_the_list_url_state()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);

        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(CsvRow("2026-09-16T10:00:00Z", "Screened", "screened@example.com", "No")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        using var oldestImport = await ImportEmailAsync(
            client,
            vacancyLocation,
            roundId,
            "oldest@example.com",
            "Fri, 28 Aug 2026 10:00:00 +0000");
        Assert.Equal(HttpStatusCode.OK, oldestImport.StatusCode);
        using var newestImport = await ImportEmailAsync(
            client,
            vacancyLocation,
            roundId,
            "newest@example.com",
            "Sun, 30 Aug 2026 10:00:00 +0000");
        Assert.Equal(HttpStatusCode.OK, newestImport.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));
        var allPage = await GetPageAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all");
        var newest = Assert.Single(
            allPage.Items,
            candidate => candidate.SourceSenderEmail == "newest@example.com");
        await UpdateReviewAsync(
            client,
            vacancyLocation,
            roundId,
            newest.Id,
            "shortlisted");

        var queue = await GetReviewQueueAsync(client, vacancyLocation, roundId);
        Assert.Equal(2, queue.Count);
        Assert.Equal("newest@example.com", queue[0].SourceSenderEmail);
        Assert.Equal("oldest@example.com", queue[1].SourceSenderEmail);

        var withScreened = await GetReviewQueueAsync(
            client,
            vacancyLocation,
            roundId,
            "?screened=all&sort=oldest");
        Assert.Equal(3, withScreened.Count);
        Assert.Equal("oldest@example.com", withScreened[0].SourceSenderEmail);
        Assert.Equal("newest@example.com", withScreened[1].SourceSenderEmail);
        var screened = Assert.Single(withScreened, candidate => candidate.ScreenedOut);
        Assert.Equal("screened@example.com", screened.ContactEmail);
        Assert.Equal("Availability · equals \"no\"", Assert.Single(screened.FiredRules).Display);

        var statusFiltered = await GetReviewQueueAsync(
            client,
            vacancyLocation,
            roundId,
            "?status=shortlisted");
        var shortlistedOnly = Assert.Single(statusFiltered);
        Assert.Equal("newest@example.com", shortlistedOnly.SourceSenderEmail);

        var queryFiltered = await GetReviewQueueAsync(
            client,
            vacancyLocation,
            roundId,
            "?query=Screened&screened=all");
        var queried = Assert.Single(queryFiltered);
        Assert.Equal("screened@example.com", queried.ContactEmail);
    }

    [Fact]
    public async Task Messaging_summary_returns_the_full_round_unpaged_with_screening_fields()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);
        var rows = Enumerable.Range(1, 101)
            .Select(index => CsvRow(
                $"2026-09-16T{index % 24:00}:00:00Z",
                $"Candidate {index}",
                $"candidate{index}@example.com",
                index == 101 ? "No" : "Yes"))
            .ToArray();
        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(rows));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        using var emailImport = await ImportEmailAsync(
            client,
            vacancyLocation,
            roundId,
            "email@example.com");
        Assert.Equal(HttpStatusCode.OK, emailImport.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));

        var summary = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/messaging-summary");
        Assert.NotNull(summary);
        Assert.Equal(102, summary.Count);
        var screened = Assert.Single(summary, candidate => candidate.ScreenedOut);
        Assert.Equal("candidate101@example.com", screened.ContactEmail);
        Assert.Equal("new", screened.ReviewStatus);
        Assert.Equal("Availability · equals \"no\"", Assert.Single(screened.FiredRules).Display);
        Assert.Contains(summary, candidate => candidate.IntakeSource == "email");
    }

    private static async Task<(string Location, long RoundId)> CreateConfiguredVacancyAsync(
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
        var roundId = Assert.Single(vacancy.Rounds).Id;

        using var setupResponse = await ImportFormAsync(
            client,
            location,
            roundId,
            Csv(CsvRow("2026-09-16T09:00:00Z", "Setup", "setup@example.com", "Setup")),
            LayoutJson());
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setupCandidate = await GetSingleCandidateAsync(
            client,
            location,
            roundId,
            "?screened=all");
        using var deleteResponse = await client.DeleteAsync(
            CandidatePath(location, roundId, setupCandidate.Id));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        return (location, roundId);
    }

    private static async Task UpsertRulesAsync(
        HttpClient client,
        string vacancyLocation,
        params (int Ordinal, string Operator, string? Value)[] rules)
    {
        using var response = await client.PutAsJsonAsync(
            ScreeningRulesPath(vacancyLocation),
            new
            {
                rules = rules
                    .Select(rule => Rule(rule.Ordinal, rule.Operator, rule.Value))
                    .ToArray()
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<CandidateListEnvelope> GetPageAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string query = "") =>
        (await client.GetFromJsonAsync<CandidateListEnvelope>(
            $"{vacancyLocation}/rounds/{roundId}/candidates{query}"))!;

    private static async Task<IReadOnlyList<CandidateSummary>> GetReviewQueueAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string query = "") =>
        (await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/review-queue{query}"))!;

    private static async Task<CandidateSummary> GetSingleCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string query = "")
    {
        var page = await GetPageAsync(client, vacancyLocation, roundId, query);
        return Assert.Single(page.Items);
    }

    private static async Task UpdateReviewAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId,
        string reviewStatus)
    {
        using var response = await client.PutAsJsonAsync(
            $"{CandidatePath(vacancyLocation, roundId, candidateId)}/review",
            new { reviewStatus, notes = "Review note" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 400 but received {(int)response.StatusCode}: {body}");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.NotEmpty(problem.Errors);
    }

    private static async Task<HttpResponseMessage> ImportFormAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string csv,
        string? layout = null)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "responses.csv");
        if (layout is not null)
        {
            form.Add(new StringContent(layout), "layout");
        }

        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import-form",
            form);
    }

    private static async Task<HttpResponseMessage> ImportEmailAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string senderEmail,
        string sentAt = "Sat, 29 Aug 2026 10:00:00 +0000")
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateEml(senderEmail, sentAt));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", "candidate.eml");
        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import",
            form);
    }

    private static object Rule(int ordinal, string @operator, string? value) =>
        new { ordinal, @operator, value };

    private static string ScreeningRulesPath(string vacancyLocation, string? suffix = null) =>
        $"{vacancyLocation}/screening-rules{(suffix is null ? string.Empty : $"/{suffix}")}";

    private static string CandidatePath(
        string vacancyLocation,
        long roundId,
        long candidateId) =>
        $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}";

    private static string Csv(params string[] rows) =>
        $"Timestamp,Name,Email,Availability\r\n{string.Join("\r\n", rows)}\r\n";

    private static string CsvRow(params string?[] cells) =>
        string.Join(",", cells.Select(cell => $"\"{(cell ?? string.Empty).Replace("\"", "\"\"")}\""));

    private static string LayoutJson() => JsonSerializer.Serialize(new
    {
        columns = new[]
        {
            new { ordinal = 1, role = (string?)"name", label = (string?)null },
            new { ordinal = 2, role = (string?)"contactEmail", label = (string?)null },
            new { ordinal = 3, role = (string?)null, label = (string?)"Availability" }
        }
    });

    private static byte[] CreateEml(
        string senderEmail,
        string sentAt = "Sat, 29 Aug 2026 10:00:00 +0000")
    {
        const string boundary = "hr-sat-screening-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var builder = new StringBuilder();
        builder.Append($"From: Applicant <{senderEmail}>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append($"Date: {sentAt}\r\n");
        builder.Append("Subject: Candidate application\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
        builder.Append("Application for the vacancy.\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: application/pdf; name=\"candidate.pdf\"\r\n");
        builder.Append("Content-Disposition: attachment; filename=\"candidate.pdf\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        builder.Append(Convert.ToBase64String(pdf));
        builder.Append($"\r\n--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private sealed record VacancyResponse(IReadOnlyList<RoundResponse> Rounds);

    private sealed record RoundResponse(long Id);

    private sealed record VacancyDetails(VacancyProgress Progress);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);

    private sealed record CandidateListEnvelope(
        IReadOnlyList<CandidateSummary> Items,
        int Page,
        int PageSize,
        int Total,
        int FilteredTotal,
        CandidateCounts Counts);

    private sealed record CandidateCounts(
        CandidateStatusCounts Status,
        CandidateOutcomeCounts Outcome,
        int ScreenedOut);

    private sealed record CandidateStatusCounts(int New, int Flagged, int Shortlisted, int Rejected);

    private sealed record CandidateOutcomeCounts(
        int Any,
        int Undecided,
        int Hired,
        int Runaway,
        int Declined);

    private sealed record CandidateSummary(
        long Id,
        string? ContactEmail,
        string? SourceSenderEmail,
        string IntakeSource,
        string ReviewStatus,
        bool IsResubmitted,
        bool ScreenedOut,
        IReadOnlyList<FiredRule> FiredRules);

    private sealed record FiredRule(int Index, string Display);

    private sealed record CandidateDetailsResponse(
        long Id,
        bool IsResubmitted,
        IReadOnlyList<FormResponse> FormResponses,
        CandidateScreening? Screening);

    private sealed record FormResponse(IReadOnlyList<string> Cells, bool IsCurrent);

    private sealed record CandidateScreening(
        bool ScreenedOut,
        IReadOnlyList<FiredRule> FiredRules);

    private sealed record PreviewResponse(
        int Total,
        int ScreenedOut,
        IReadOnlyList<PreviewRule> PerRule);

    private sealed record PreviewRule(int Index, int ScreenedOut);

    private sealed record ProblemResponse(string? Title, string? Detail);

    private sealed record ValidationProblemResponse(
        IReadOnlyDictionary<string, string[]> Errors);

    private sealed record ScreeningRuleSetResponse(
        long Id,
        long VacancyId,
        IReadOnlyList<ScreeningRuleResponse> Rules);

    private sealed record ScreeningRuleResponse(int Ordinal, string Operator, string? Value);
}