using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using hr_sat.Application.Abstractions.Email;
using NSubstitute;
using Xunit;

namespace hr_sat.Tests.Dispatches;

public sealed class SendToAllTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Send_to_all_should_render_send_and_report_per_candidate_then_be_idempotent() // US-19: HR sends the prepared messages to every contactable candidate in the round
    {
        var sender = CreateEmailSender();
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var alice = await ImportCandidateAsync(client, vacancyLocation, roundId, "Alice Applicant");
        var bob = await ImportCandidateAsync(client, vacancyLocation, roundId, "Bob Candidate");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, alice.Id, "Alice Applicant", "alice.applicant@example.com");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, bob.Id, "Bob Candidate", "bob.candidate@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, alice.Id, "shortlisted");
        await UpdateReviewAsync(client, vacancyLocation, roundId, bob.Id, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "shortlisted",
            "Welcome {{candidate_name}}",
            "The {{vacancy_title}} team would like to meet you.");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Not this time {{candidate_name}}",
            "Thank you for applying to {{vacancy_title}}.");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(report);
        Assert.NotNull(report.RunId);
        Assert.Equal(2, report.SentCount);
        Assert.Equal(0, report.FailedCount);
        Assert.Equal(0, report.ExcludedCount);
        Assert.Equal(2, report.Outcomes.Count);
        var aliceOutcome = Assert.Single(report.Outcomes, outcome => outcome.CandidateId == alice.Id);
        Assert.Equal("shortlisted", aliceOutcome.TemplateKind);
        Assert.Equal("sent", aliceOutcome.Status);
        Assert.Null(aliceOutcome.Error);
        var bobOutcome = Assert.Single(report.Outcomes, outcome => outcome.CandidateId == bob.Id);
        Assert.Equal("rejected", bobOutcome.TemplateKind);
        Assert.Equal("sent", bobOutcome.Status);
        await sender.Received(1).SendAsync(
            "alice.applicant@example.com",
            "Welcome Alice Applicant",
            "The Data Analyst team would like to meet you.",
            Arg.Any<CancellationToken>());
        await sender.Received(1).SendAsync(
            "bob.candidate@example.com",
            "Not this time Bob Candidate",
            "Thank you for applying to Data Analyst.",
            Arg.Any<CancellationToken>());

        var summary = await GetMessagingSummaryAsync(client, vacancyLocation, roundId);
        Assert.Equal(2, summary.Count);
        Assert.All(summary, item =>
        {
            Assert.Equal("contactable", item.Contactability);
            Assert.NotNull(item.LastDispatch);
            Assert.Equal("sent", item.LastDispatch.Status);
            Assert.NotEqual(default, item.LastDispatch.At);
        });

        // A successful Dispatch per Candidate is recorded once, ever: the second run
        // has nobody to send to and returns no run id.
        var secondResponse = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var secondReport = await secondResponse.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(secondReport);
        Assert.Null(secondReport.RunId);
        Assert.Equal(0, secondReport.SentCount);
        Assert.Equal(0, secondReport.FailedCount);
        await sender.Received(2).SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_to_all_should_record_the_failure_and_continue_when_one_recipient_fails() // US-19 — one bad address never blocks the round
    {
        var sender = CreateEmailSender();
        sender.SendAsync(
                "fail@example.com",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("SMTP offline.")));
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var failing = await ImportCandidateAsync(client, vacancyLocation, roundId, "Failing Candidate");
        var passing = await ImportCandidateAsync(client, vacancyLocation, roundId, "Passing Candidate");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, failing.Id, "Failing Candidate", "fail@example.com");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, passing.Id, "Passing Candidate", "pass@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, failing.Id, "rejected");
        await UpdateReviewAsync(client, vacancyLocation, roundId, passing.Id, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Not this time {{candidate_name}}",
            "Thank you for applying to {{vacancy_title}}.");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(report);
        Assert.NotNull(report.RunId);
        Assert.Equal(1, report.SentCount);
        Assert.Equal(1, report.FailedCount);
        var failure = Assert.Single(report.Outcomes, outcome => outcome.CandidateId == failing.Id);
        Assert.Equal("failed", failure.Status);
        Assert.Equal("SMTP offline.", failure.Error);
        var success = Assert.Single(report.Outcomes, outcome => outcome.CandidateId == passing.Id);
        Assert.Equal("sent", success.Status);

        var summary = await GetMessagingSummaryAsync(client, vacancyLocation, roundId);
        Assert.Equal(
            "failed",
            Assert.Single(summary, item => item.Id == failing.Id).LastDispatch?.Status);
        Assert.Equal(
            "sent",
            Assert.Single(summary, item => item.Id == passing.Id).LastDispatch?.Status);
    }

    [Fact]
    public async Task Send_to_all_should_refuse_and_record_nothing_when_smtp_is_not_configured() // US-19 — dispatch requires a configured per-installation SMTP (ADR-0019)
    {
        var sender = CreateEmailSender(configured: false);
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var candidate = await ImportCandidateAsync(client, vacancyLocation, roundId, "Alice Applicant");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, candidate.Id, "Alice Applicant", "alice.applicant@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, candidate.Id, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Not this time {{candidate_name}}",
            "Thank you for applying to {{vacancy_title}}.");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Dispatch.SmtpNotConfigured", problem.Title);
        await sender.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var summary = await GetMessagingSummaryAsync(client, vacancyLocation, roundId);
        Assert.All(summary, item => Assert.Null(item.LastDispatch));
    }

    [Fact]
    public async Task Send_to_all_should_refuse_before_any_send_when_the_needed_template_is_missing() // US-19 — a missing template blocks the whole send, never a partial silent send
    {
        var sender = CreateEmailSender();
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var candidate = await ImportCandidateAsync(client, vacancyLocation, roundId, "Alice Applicant");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, candidate.Id, "Alice Applicant", "alice.applicant@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, candidate.Id, "rejected");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("Dispatch.MissingTemplate", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal("rejected", problem.RootElement.GetProperty("kind").GetString());
        await sender.DidNotReceive().SendAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_to_all_should_reject_a_closed_vacancy_with_conflict() // domain: Closed Vacancy
    {
        factory.EmailSender = CreateEmailSender();
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        using var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Dispatch.VacancyClosed", problem.Title);
    }

    [Fact]
    public async Task Send_to_all_should_report_an_unknown_vacancy() // domain: Dispatch Run
    {
        factory.EmailSender = CreateEmailSender();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/vacancies/999999/rounds/1/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Vacancies.NotFound", problem.Title);
    }

    [Fact]
    public async Task Send_to_all_should_report_an_unknown_round() // domain: Intake Round
    {
        factory.EmailSender = CreateEmailSender();
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client);

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/999999/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("IntakeRounds.NotFound", problem.Title);
    }

    [Fact]
    public async Task Send_to_all_should_never_email_or_report_screened_out_candidates() // US-19 — Screened Out candidates are filtered before classification and never appear in the report
    {
        var sender = CreateEmailSender();
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateConfiguredVacancyAsync(client);
        using var import = await ImportFormAsync(
            client,
            vacancyLocation,
            roundId,
            Csv(
                CsvRow("2026-09-16T09:00:00Z", "Screened Person", "screened@example.com", "No"),
                CsvRow("2026-09-16T10:00:00Z", "Passing Person", "passes@example.com", "Yes")));
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        await UpsertRulesAsync(client, vacancyLocation, (3, "equals", "no"));
        var imported = await GetMessagingSummaryAsync(client, vacancyLocation, roundId);
        var screenedId = Assert.Single(imported, item => item.ContactEmail == "screened@example.com").Id;
        var passesId = Assert.Single(imported, item => item.ContactEmail == "passes@example.com").Id;
        await UpdateReviewAsync(client, vacancyLocation, roundId, screenedId, "rejected");
        await UpdateReviewAsync(client, vacancyLocation, roundId, passesId, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Not this time {{candidate_name}}",
            "Thank you for applying to {{vacancy_title}}.");

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(report);
        Assert.Equal(1, report.SentCount);
        Assert.Equal(0, report.ExcludedCount);
        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal(passesId, outcome.CandidateId);
        Assert.DoesNotContain(report.Outcomes, item => item.CandidateId == screenedId);
        Assert.DoesNotContain(report.Excluded, item => item.CandidateId == screenedId);
        await sender.Received(1).SendAsync(
            "passes@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await sender.DidNotReceive().SendAsync(
            "screened@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_to_all_should_fail_fast_when_a_run_is_already_in_progress() // domain: Dispatch Run — one run per round at a time
    {
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = Substitute.For<IEmailSender>();
        sender.IsConfigured.Returns(true);
        sender.SendAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(async _ =>
            {
                sendStarted.TrySetResult();
                await releaseSend.Task;
            });
        factory.EmailSender = sender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var candidate = await ImportCandidateAsync(client, vacancyLocation, roundId, "Alice Applicant");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, candidate.Id, "Alice Applicant", "alice.applicant@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, candidate.Id, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Not this time {{candidate_name}}",
            "Thank you for applying to {{vacancy_title}}.");

        var firstRequest = client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);
        // The first request holds the run lock once the send starts; no sleeps, the
        // substitute's gate is the signal.
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var secondResponse = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Dispatch.RunInProgress", problem.Title);

        releaseSend.TrySetResult();
        var firstResponse = await firstRequest;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstReport = await firstResponse.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(firstReport);
        Assert.Equal(1, firstReport.SentCount);
    }

    private static IEmailSender CreateEmailSender(bool configured = true)
    {
        var sender = Substitute.For<IEmailSender>();
        sender.IsConfigured.Returns(configured);
        sender.SendAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.CompletedTask);
        return sender;
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
        var location = response.Headers.Location!.OriginalString;
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyDetails>();
        Assert.NotNull(vacancy);
        return (location, Assert.Single(vacancy.Rounds).Id);
    }

    private static async Task<(string Location, long RoundId)> CreateConfiguredVacancyAsync(
        HttpClient client)
    {
        var (location, roundId) = await CreateVacancyAsync(client);
        using var setupResponse = await ImportFormAsync(
            client,
            location,
            roundId,
            Csv(CsvRow("2026-09-16T08:00:00Z", "Setup", "setup@example.com", "Setup")),
            LayoutJson());
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var page = await client.GetFromJsonAsync<CandidateListEnvelope>(
            $"{location}/rounds/{roundId}/candidates");
        Assert.NotNull(page);
        var setupCandidate = Assert.Single(page.Items);
        using var deleteResponse = await client.DeleteAsync(
            $"{location}/rounds/{roundId}/candidates/{setupCandidate.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        return (location, roundId);
    }

    private static async Task<ImportedCandidate> ImportCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string senderName)
    {
        using var form = new MultipartFormDataContent();
        var eml = CreateEml(
            senderName,
            $"{senderName.Replace(' ', '.').ToLowerInvariant()}@mail.example.com",
            $"{senderName} application",
            "Please review my application.");
        var fileContent = new ByteArrayContent(eml);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", "candidate.eml");

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

    private static async Task UpsertRulesAsync(
        HttpClient client,
        string vacancyLocation,
        params (int Ordinal, string Operator, string? Value)[] rules)
    {
        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/screening-rules",
            new
            {
                rules = rules
                    .Select(rule => new { ordinal = rule.Ordinal, @operator = rule.Operator, value = rule.Value })
                    .ToArray()
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UpsertTemplateAsync(
        HttpClient client,
        string vacancyLocation,
        string kind,
        string subject,
        string body)
    {
        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/{kind}",
            new { subject, body });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UpdateReviewAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId,
        string reviewStatus)
    {
        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}/review",
            new { reviewStatus, notes = "Review note" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task UpdateDetailsAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        long candidateId,
        string fullName,
        string contactEmail)
    {
        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/{candidateId}/details",
            new { fullName, contactEmail });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<IReadOnlyList<MessagingSummaryItem>> GetMessagingSummaryAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId)
    {
        var summary = await client.GetFromJsonAsync<IReadOnlyList<MessagingSummaryItem>>(
            $"{vacancyLocation}/rounds/{roundId}/messaging-summary");
        Assert.NotNull(summary);
        return summary;
    }

    private static byte[] CreateEml(
        string senderName,
        string senderEmail,
        string subject,
        string body)
    {
        const string boundary = "hr-sat-dispatch-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var builder = new StringBuilder();
        builder.Append($"From: {senderName} <{senderEmail}>\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append($"Subject: {subject}\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n\r\n");
        builder.Append(body);
        builder.Append("\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: application/pdf; name=\"candidate.pdf\"\r\n");
        builder.Append("Content-Disposition: attachment; filename=\"candidate.pdf\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        builder.Append(Convert.ToBase64String(pdf));
        builder.Append($"\r\n--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

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

    private sealed record VacancyDetails(IReadOnlyList<RoundDetails> Rounds);

    private sealed record RoundDetails(long Id);

    private sealed record CandidateListEnvelope(IReadOnlyList<CandidateListItem> Items);

    private sealed record CandidateListItem(long Id);

    private sealed record ImportedCandidate(long Id);

    private sealed record ImportResponse(IReadOnlyList<ImportFileResponse> Results);

    private sealed record ImportFileResponse(string Status, ImportedCandidate? Candidate);

    private sealed record DispatchReport(
        long? RunId,
        int SentCount,
        int FailedCount,
        int ExcludedCount,
        IReadOnlyList<DispatchOutcome> Outcomes,
        IReadOnlyList<DispatchExclusion> Excluded);

    private sealed record DispatchOutcome(
        long CandidateId,
        string CandidateName,
        string TemplateKind,
        string Status,
        string? Error);

    private sealed record DispatchExclusion(long CandidateId, string CandidateName, string Reason);

    private sealed record MessagingSummaryItem(
        long Id,
        string? ContactEmail,
        string Contactability,
        LastDispatchResponse? LastDispatch);

    private sealed record LastDispatchResponse(string Status, DateTimeOffset At);

    private sealed record ProblemResponse(string? Title);
}
