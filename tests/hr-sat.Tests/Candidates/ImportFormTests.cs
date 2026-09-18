using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using hr_sat.Application.Features.Candidates;
using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Application.Features.FormLayouts;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ImportFormTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Importing_csv_returns_summary_and_persists_ordered_jsonb_form_response() // US-01: HR imports a Google Forms CSV and can review its raw response
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var importResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(
                "\"2026-09-16T10:00:00Z\",\"Alice\nApplicant\",\" Alice@EXAMPLE.com \""));

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var summary = await importResponse.Content.ReadFromJsonAsync<ImportFormResponse>();
        Assert.NotNull(summary);
        Assert.Equal(new ImportFormResponse(1, 1, 0, 0, 0), summary);

        var candidate = await GetSingleCandidateAsync(client, vacancyLocation, roundId);
        Assert.Equal("form", candidate.IntakeSource);
        Assert.False(candidate.IsResubmitted);
        var details = await GetDetailsAsync(client, vacancyLocation, roundId, candidate.Id);
        Assert.Equal("form", details.IntakeSource);
        Assert.Null(details.SourceOriginalFilename);
        var response = Assert.Single(details.FormResponses);
        Assert.Equal(
            new[] { "2026-09-16T10:00:00Z", "Alice\nApplicant", " Alice@EXAMPLE.com " },
            response.Cells);
        Assert.True(response.IsCurrent);

        using var detailsResponse = await client.GetAsync(
            CandidatePath(vacancyLocation, roundId, candidate.Id));
        detailsResponse.EnsureSuccessStatusCode();
        using var detailsJson = JsonDocument.Parse(
            await detailsResponse.Content.ReadAsStringAsync());
        var formResponseJson =
            detailsJson.RootElement.GetProperty("formResponses")[0];
        Assert.False(formResponseJson.TryGetProperty("identityKey", out _));
    }

    [Fact]
    public async Task Importing_csv_without_a_valid_form_layout_returns_a_conflict_problem() // domain: Form Layout is required before form response import
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, configureLayout: false);

        using var response = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Applicant\",\"person@example.com\""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "Candidates.FormLayoutRequired");
        using var problemJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "Timestamp", "Name", "Email" },
            problemJson.RootElement
                .GetProperty("headers")
                .EnumerateArray()
                .Select(header => header.GetString())
                .ToArray());
    }

    [Fact]
    public async Task First_upload_with_layout_completes_guided_setup_and_prefills_candidate_details()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, configureLayout: false);

        using var response = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Guided Applicant\",\"guided@example.com\""),
            layout: LayoutJson());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ImportFormResponse>();
        Assert.NotNull(summary);
        Assert.Equal(new ImportFormResponse(1, 1, 0, 0, 0), summary);

        using var layoutResponse = await client.GetAsync($"{vacancyLocation}/form-layout");
        Assert.Equal(HttpStatusCode.OK, layoutResponse.StatusCode);
        var layout = await layoutResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(layout);
        Assert.Equal(new[] { "Timestamp", "Name", "Email" }, layout.HeaderSnapshot);
        Assert.True(layout.IsValid);

        var candidate = await GetSingleCandidateAsync(client, vacancyLocation, roundId);
        var details = await GetDetailsAsync(client, vacancyLocation, roundId, candidate.Id);
        Assert.Equal("Guided Applicant", details.FullName);
        Assert.Equal("guided@example.com", details.ContactEmail);
    }

    [Fact]
    public async Task Saving_a_form_layout_without_name_and_email_returns_a_validation_problem() // domain: a Form Layout is invalid without Name and Contact Email bindings
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/form-layout",
            new
            {
                columns = new[]
                {
                    new { ordinal = 1, role = "name", label = (string?)null }
                }
            });

        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest,
            await response.Content.ReadAsStringAsync());
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains(
            problem.Errors["contactEmail"],
            message => message.Contains("bound", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reuploading_a_fresher_row_returns_updated_and_preserves_review_data() // domain: Resubmitted never changes review data or typed candidate details
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using (var firstImport = await ImportFormAsync(
                   client,
                   vacancyLocation,
                   roundId,
                   Csv("\"2026-09-16T10:00:00Z\",\"Original\",\"person@example.com\"")))
        {
            Assert.Equal(HttpStatusCode.OK, firstImport.StatusCode);
        }

        var candidate = await GetSingleCandidateAsync(client, vacancyLocation, roundId);
        var candidatePath = CandidatePath(vacancyLocation, roundId, candidate.Id);
        using var detailsResponse = await client.PutAsJsonAsync(
            $"{candidatePath}/details",
            new
            {
                fullName = "Typed Name",
                contactEmail = "typed@example.com",
                contactPhone = "555-0100"
            });
        detailsResponse.EnsureSuccessStatusCode();
        using var reviewResponse = await client.PutAsJsonAsync(
            $"{candidatePath}/review",
            new { reviewStatus = "flagged", notes = "Review note" });
        reviewResponse.EnsureSuccessStatusCode();

        using var reuploadResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T11:00:00Z\",\"Updated\",\"PERSON@example.com\""));

        Assert.Equal(HttpStatusCode.OK, reuploadResponse.StatusCode);
        var summary = await reuploadResponse.Content.ReadFromJsonAsync<ImportFormResponse>();
        Assert.NotNull(summary);
        Assert.Equal(new ImportFormResponse(1, 0, 1, 0, 0), summary);

        var details = await GetDetailsAsync(client, vacancyLocation, roundId, candidate.Id);
        Assert.Equal("Typed Name", details.FullName);
        Assert.Equal("typed@example.com", details.ContactEmail);
        Assert.Equal("555-0100", details.ContactPhone);
        Assert.Equal("flagged", details.ReviewStatus);
        Assert.Equal("Review note", details.Notes);
        Assert.True(details.IsResubmitted);
        Assert.Equal(2, details.FormResponses.Count);
        Assert.Equal(
            "Updated",
            Assert.Single(details.FormResponses, formResponse => formResponse.IsCurrent).Cells[1]);
        Assert.Contains(details.FormResponses, formResponse => !formResponse.IsCurrent);
    }

    [Fact]
    public async Task Reuploading_changed_mapped_headers_returns_drift_and_confirm_adopts_snapshot()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var saveLayoutResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/form-layout",
            new
            {
                columns = new[]
                {
                    new { ordinal = 1, role = "name", label = "Candidate name" },
                    new { ordinal = 2, role = "contactEmail", label = "Email address" }
                }
            });
        Assert.Equal(HttpStatusCode.OK, saveLayoutResponse.StatusCode);

        using var initialImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Original\",\"person@example.com\""));
        Assert.Equal(HttpStatusCode.OK, initialImport.StatusCode);

        var changedCsv = CsvWithHeaders(
            "Timestamp,Applicant name,Email",
            "\"2026-09-16T11:00:00Z\",\"Updated\",\"person@example.com\"");
        using var driftResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            changedCsv);

        Assert.Equal(HttpStatusCode.Conflict, driftResponse.StatusCode);
        using var driftJson = JsonDocument.Parse(await driftResponse.Content.ReadAsStringAsync());
        Assert.Equal("Candidates.FormHeaderDrift", driftJson.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            new[] { "Timestamp", "Applicant name", "Email" },
            driftJson.RootElement
                .GetProperty("headers")
                .EnumerateArray()
                .Select(header => header.GetString())
                .ToArray());
        var change = Assert.Single(driftJson.RootElement.GetProperty("changes").EnumerateArray());
        Assert.Equal(1, change.GetProperty("ordinal").GetInt32());
        Assert.Equal("Name", change.GetProperty("was").GetString());
        Assert.Equal("Applicant name", change.GetProperty("now").GetString());

        using var confirmedResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            changedCsv,
            confirmDrift: true);
        Assert.Equal(HttpStatusCode.OK, confirmedResponse.StatusCode);

        using var layoutResponse = await client.GetAsync($"{vacancyLocation}/form-layout");
        Assert.Equal(HttpStatusCode.OK, layoutResponse.StatusCode);
        var layout = await layoutResponse.Content.ReadFromJsonAsync<FormLayoutResponse>();
        Assert.NotNull(layout);
        Assert.Equal(new[] { "Timestamp", "Applicant name", "Email" }, layout.HeaderSnapshot);
        Assert.Equal("Candidate name", layout.Columns.Single(column => column.Ordinal == 1).Label);
        Assert.Equal("Email address", layout.Columns.Single(column => column.Ordinal == 2).Label);
    }

    [Fact]
    public async Task Confirming_drift_with_a_missing_picked_column_is_blocked()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var shortenedCsv = CsvWithHeaders(
            "Timestamp,Name",
            "\"2026-09-16T11:00:00Z\",\"Applicant\"");

        using var driftResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            shortenedCsv);
        Assert.Equal(HttpStatusCode.Conflict, driftResponse.StatusCode);
        using var driftJson = JsonDocument.Parse(await driftResponse.Content.ReadAsStringAsync());
        var change = Assert.Single(driftJson.RootElement.GetProperty("changes").EnumerateArray());
        Assert.Equal(2, change.GetProperty("ordinal").GetInt32());
        Assert.Equal("Email", change.GetProperty("was").GetString());
        Assert.Equal("column no longer present", change.GetProperty("now").GetString());

        using var confirmedResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            shortenedCsv,
            confirmDrift: true);
        Assert.Equal(HttpStatusCode.BadRequest, confirmedResponse.StatusCode);
        await AssertProblemAsync(confirmedResponse, "FormLayouts.Invalid");

        var candidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/candidates");
        Assert.NotNull(candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Importing_into_a_closed_round_returns_a_conflict_problem() // domain: a closed Intake Round is read-only
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{roundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        using var response = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Closed\",\"person@example.com\""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "IntakeRounds.Closed");
    }

    [Fact]
    public async Task Importing_into_a_closed_vacancy_returns_a_lifecycle_conflict() // domain: a closed Vacancy cannot receive candidate imports
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var closeResponse = await client.PostAsync(
            $"{vacancyLocation}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        using var response = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Closed\",\"person@example.com\""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "Candidates.FormImportLifecycleConflict");
    }

    [Fact]
    public async Task Importing_a_ragged_csv_returns_a_validation_problem_naming_the_row() // domain: malformed Form Response imports are rejected
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var response = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            "Timestamp,Name,Email\r\n\"2026-09-16T10:00:00Z\",\"Missing email\"\r\n");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors["file"], message => message.Contains("row 2", StringComparison.OrdinalIgnoreCase));

        var candidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/candidates");
        Assert.NotNull(candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Reimporting_a_key_from_a_closed_round_creates_an_active_candidate_and_notices_prior_application() // domain: Prior Application Notice spans rounds within one vacancy
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, firstRoundId) = await CreateVacancyAsync(client);
        using var firstImport = await ImportFormAsync(
            client,
            vacancyLocation,
            firstRoundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Prior\",\"person@example.com\""));
        Assert.Equal(HttpStatusCode.OK, firstImport.StatusCode);
        using var closeResponse = await client.PutAsync(
            $"{vacancyLocation}/rounds/{firstRoundId}/close",
            content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        using var createRoundResponse = await client.PostAsJsonAsync(
            $"{vacancyLocation}/rounds",
            new { name = "Second wave" });
        Assert.Equal(HttpStatusCode.OK, createRoundResponse.StatusCode);
        var secondRound = await createRoundResponse.Content.ReadFromJsonAsync<RoundResponse>();
        Assert.NotNull(secondRound);

        using var secondImport = await ImportFormAsync(
            client,
            vacancyLocation,
            secondRound.Id,
            Csv("\"2026-09-17T10:00:00Z\",\"Current\",\"PERSON@example.com\""));

        Assert.Equal(HttpStatusCode.OK, secondImport.StatusCode);
        var summary = await secondImport.Content.ReadFromJsonAsync<ImportFormResponse>();
        Assert.NotNull(summary);
        Assert.Equal(new ImportFormResponse(1, 1, 0, 0, 1), summary);
        var secondCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{secondRound.Id}/candidates");
        Assert.NotNull(secondCandidates);
        Assert.Single(secondCandidates);
    }

    [Fact]
    public async Task Form_identity_matching_an_email_sender_counts_as_a_prior_application() // domain: Prior Application Notice matches identifying email across intake sources
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var emailImport = await ImportEmailAsync(
            client,
            vacancyLocation,
            roundId,
            "person@example.com");
        Assert.Equal(HttpStatusCode.OK, emailImport.StatusCode);

        using var formImport = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Form applicant\",\"PERSON@example.com\""));

        Assert.Equal(HttpStatusCode.OK, formImport.StatusCode);
        var summary = await formImport.Content.ReadFromJsonAsync<ImportFormResponse>();
        Assert.NotNull(summary);
        Assert.Equal(1, summary.PriorApplications);
    }

    [Fact]
    public async Task Form_candidate_can_be_removed_without_a_source_file_deletion_key() // domain: Candidate Removal applies to Form Response candidates without email storage
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var importResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Applicant\",\"person@example.com\""));
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var candidate = await GetSingleCandidateAsync(client, vacancyLocation, roundId);

        using var deleteResponse = await client.DeleteAsync(
            CandidatePath(vacancyLocation, roundId, candidate.Id));

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var candidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/candidates");
        Assert.NotNull(candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Purging_a_vacancy_with_a_form_candidate_succeeds_without_source_file_cleanup() // domain: Purge removes all candidate information owned by a vacancy
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var importResponse = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv("\"2026-09-16T10:00:00Z\",\"Applicant\",\"person@example.com\""));
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        using var purgeResponse = await client.DeleteAsync(vacancyLocation);

        Assert.Equal(HttpStatusCode.NoContent, purgeResponse.StatusCode);
        using var getResponse = await client.GetAsync(vacancyLocation);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private static async Task<(string Location, long RoundId)> CreateVacancyAsync(
        HttpClient client,
        bool configureLayout = true)
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
        if (configureLayout)
        {
            using var layoutResponse = await ImportFormAsync(
                client,
                location,
                Assert.Single(vacancy.Rounds).Id,
                Csv("\"2026-09-16T09:00:00Z\",\"Setup applicant\",\"setup@example.com\""),
                layout: LayoutJson());
            Assert.True(
                layoutResponse.StatusCode == HttpStatusCode.OK,
                await layoutResponse.Content.ReadAsStringAsync());
            var setupCandidate = await GetSingleCandidateAsync(
                client,
                location,
                Assert.Single(vacancy.Rounds).Id);
            using var deleteResponse = await client.DeleteAsync(
                CandidatePath(location, Assert.Single(vacancy.Rounds).Id, setupCandidate.Id));
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        return (location, Assert.Single(vacancy.Rounds).Id);
    }

    private static async Task<HttpResponseMessage> ImportFormAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string csv,
        string fileName = "responses.csv",
        string? layout = null,
        bool confirmDrift = false)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", fileName);
        if (layout is not null)
        {
            form.Add(new StringContent(layout), "layout");
        }

        if (confirmDrift)
        {
            form.Add(new StringContent("true"), "confirmDrift");
        }

        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import-form",
            form);
    }

    private static async Task<HttpResponseMessage> ImportEmailAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string senderEmail)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateEml(senderEmail));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", "candidate.eml");
        return await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import",
            form);
    }

    private static async Task<CandidateSummary> GetSingleCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId)
    {
        var candidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/rounds/{roundId}/candidates");
        Assert.NotNull(candidates);
        return Assert.Single(candidates);
    }

    private static async Task<CandidateDetailsResponse> GetDetailsAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId) =>
        (await client.GetFromJsonAsync<CandidateDetailsResponse>(
            CandidatePath(vacancyLocation, roundId, candidateId)))!;

    private static string CandidatePath(string vacancyLocation, long roundId, long candidateId) =>
        $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}";

    private static async Task AssertProblemAsync(HttpResponseMessage response, string title)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal(title, problem.Title);
        Assert.Equal((int)response.StatusCode, problem.Status);
    }

    private static string Csv(params string[] rows) =>
        $"Timestamp,Name,Email\r\n{string.Join("\r\n", rows)}\r\n";

    private static string CsvWithHeaders(string headers, params string[] rows) =>
        $"{headers}\r\n{string.Join("\r\n", rows)}\r\n";

    private static string LayoutJson() => JsonSerializer.Serialize(new
    {
        columns = new[]
        {
            new { ordinal = 1, role = "name", label = (string?)null },
            new { ordinal = 2, role = "contactEmail", label = (string?)null }
        }
    });

    private static byte[] CreateEml(string senderEmail)
    {
        const string boundary = "hr-sat-form-import-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var builder = new StringBuilder();
        builder.Append($"From: Applicant <{senderEmail}>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append("Subject: Candidate application\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
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

    private sealed record RoundResponse(long Id, int RoundNumber, string Status);

    private sealed record CandidateSummary(long Id, string IntakeSource, bool IsResubmitted);

    private sealed record ProblemResponse(string? Title, int? Status, string? Detail);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}