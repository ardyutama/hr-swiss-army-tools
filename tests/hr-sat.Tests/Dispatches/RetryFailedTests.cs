using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using hr_sat.Application.Abstractions.Email;
using NSubstitute;
using Xunit;

namespace hr_sat.Tests.Dispatches;

public sealed class RetryFailedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Retry_should_resend_the_recorded_snapshot_even_after_the_template_changes() // US-19 — a retry resends what the run recorded; later template edits never rewrite it
    {
        var firstSender = CreateEmailSender();
        firstSender.SendAsync(
                "fail@example.com",
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("SMTP offline.")));
        factory.EmailSender = firstSender;
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);
        var passing = await ImportCandidateAsync(client, vacancyLocation, roundId, "Passing Candidate");
        var failing = await ImportCandidateAsync(client, vacancyLocation, roundId, "Fail Candidate");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, passing.Id, "Passing Candidate", "pass@example.com");
        await UpdateDetailsAsync(
            client, vacancyLocation, roundId, failing.Id, "Fail Candidate", "fail@example.com");
        await UpdateReviewAsync(client, vacancyLocation, roundId, passing.Id, "rejected");
        await UpdateReviewAsync(client, vacancyLocation, roundId, failing.Id, "rejected");
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Hello {{candidate_name}}",
            "Version one for {{vacancy_title}}.");

        var firstResponse = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches",
            content: null);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var firstReport = await firstResponse.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(firstReport);
        Assert.NotNull(firstReport.RunId);
        Assert.Equal(1, firstReport.SentCount);
        Assert.Equal(1, firstReport.FailedCount);

        // The template moves on; the failed row's snapshot must not.
        await UpsertTemplateAsync(
            client,
            vacancyLocation,
            "rejected",
            "Hello {{candidate_name}}",
            "Version two for {{vacancy_title}}.");
        var retrySender = CreateEmailSender();
        factory.EmailSender = retrySender;

        var retryResponse = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches/{firstReport.RunId}/retry",
            content: null);

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retryReport = await retryResponse.Content.ReadFromJsonAsync<DispatchReport>();
        Assert.NotNull(retryReport);
        Assert.Equal(firstReport.RunId, retryReport.RunId);
        Assert.Equal(2, retryReport.SentCount);
        Assert.Equal(0, retryReport.FailedCount);
        Assert.Equal(0, retryReport.ExcludedCount);
        var outcome = Assert.Single(retryReport.Outcomes);
        Assert.Equal(failing.Id, outcome.CandidateId);
        Assert.Equal("sent", outcome.Status);
        Assert.Null(outcome.Error);
        await retrySender.Received(1).SendAsync(
            "fail@example.com",
            "Hello Fail Candidate",
            "Version one for Data Analyst.",
            Arg.Any<CancellationToken>());
        await retrySender.DidNotReceive().SendAsync(
            "pass@example.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var summary = await GetMessagingSummaryAsync(client, vacancyLocation, roundId);
        Assert.Equal(2, summary.Count);
        Assert.All(summary, item => Assert.Equal("sent", item.LastDispatch?.Status));
    }

    [Fact]
    public async Task Retry_should_report_an_unknown_run() // domain: Dispatch Run — retries are run-scoped
    {
        factory.EmailSender = CreateEmailSender();
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client);

        var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/dispatches/999999/retry",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("Dispatch.RunNotFound", problem.Title);
    }

    private static IEmailSender CreateEmailSender()
    {
        var sender = Substitute.For<IEmailSender>();
        sender.CheckReadinessAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SmtpReadiness.Configured));
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
        const string boundary = "hr-sat-retry-boundary";
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

    private sealed record VacancyDetails(IReadOnlyList<RoundDetails> Rounds);

    private sealed record RoundDetails(long Id);

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
