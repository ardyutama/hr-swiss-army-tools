using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests.EmailTemplates;

public sealed class EmailTemplateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Email_templates_can_be_created_replaced_viewed_and_deleted() // US-19: HR manages one independent template per vacancy and kind
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");

        using var createResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/shortlisted",
            new
            {
                subject = "Welcome {{candidate_name}}",
                body = "The {{vacancy_title}} team would like to meet you."
            });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EmailTemplateResponse>();
        Assert.NotNull(created);
        Assert.Equal("shortlisted", created.Kind);

        using var replaceResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/shortlisted",
            new { subject = "Updated subject", body = "Updated body." });
        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        var replaced = await replaceResponse.Content.ReadFromJsonAsync<EmailTemplateResponse>();
        Assert.NotNull(replaced);
        Assert.Equal(created.Id, replaced.Id);
        Assert.Equal("Updated subject", replaced.Subject);

        using var rejectedResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/rejected",
            new { subject = "Not this time", body = "Thank you for applying." });
        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);

        var templates = await client.GetFromJsonAsync<IReadOnlyList<EmailTemplateResponse>>(
            $"{vacancyLocation}/email-templates");
        Assert.NotNull(templates);
        Assert.Equal(new[] { "shortlisted", "rejected" }, templates.Select(template => template.Kind));
        Assert.Equal("Updated body.", templates[0].Body);

        using var deleteResponse = await client.DeleteAsync(
            $"{vacancyLocation}/email-templates/shortlisted");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        templates = await client.GetFromJsonAsync<IReadOnlyList<EmailTemplateResponse>>(
            $"{vacancyLocation}/email-templates");
        Assert.NotNull(templates);
        var remaining = Assert.Single(templates);
        Assert.Equal("rejected", remaining.Kind);
    }

    [Fact]
    public async Task Email_template_writes_validate_kind_subject_and_body() // US-19: template content is complete text with one of two supported kinds
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");

        using var invalidKindResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/other",
            new { subject = "Subject", body = "Body" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidKindResponse.StatusCode);
        await AssertValidationProblemContainsAsync(invalidKindResponse, "kind");

        using var overlongSubjectResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/shortlisted",
            new { subject = new string('x', 999), body = "Body" });
        Assert.Equal(HttpStatusCode.BadRequest, overlongSubjectResponse.StatusCode);
        await AssertValidationProblemContainsAsync(overlongSubjectResponse, "subject");

        using var blankBodyResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/shortlisted",
            new { subject = "Subject", body = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blankBodyResponse.StatusCode);
        await AssertValidationProblemContainsAsync(blankBodyResponse, "body");
    }

    [Fact]
    public async Task Template_sources_exclude_current_include_closed_and_order_by_opening_date() // US-19: templates are reusable from previous vacancies, including closed vacancies
    {
        using var client = factory.CreateClient();
        var (currentLocation, _) = await CreateVacancyAsync(client, "Current role", "2026-09-10");
        var (olderLocation, _) = await CreateVacancyAsync(client, "Older role", "2026-08-01");
        var (newerLocation, _) = await CreateVacancyAsync(client, "Newer role", "2026-09-01");

        await PutTemplateAsync(client, currentLocation, "Current subject");
        await PutTemplateAsync(client, olderLocation, "Older subject");
        await PutTemplateAsync(client, newerLocation, "Newer subject");
        using var closeResponse = await client.PostAsync($"{olderLocation}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var sources = await client.GetFromJsonAsync<IReadOnlyList<EmailTemplateSourceResponse>>(
            $"{currentLocation}/email-templates/sources?kind=shortlisted");

        Assert.NotNull(sources);
        Assert.Equal(2, sources.Count);
        Assert.Equal("Newer role", sources[0].VacancyTitle);
        Assert.Equal("Newer subject", sources[0].Subject);
        Assert.Equal("Older role", sources[1].VacancyTitle);
        Assert.Equal("Older subject", sources[1].Subject);
        Assert.DoesNotContain(sources, source => source.VacancyTitle == "Current role");
    }

    [Fact]
    public async Task Template_sources_require_a_supported_kind_and_known_vacancy() // domain: a source is another vacancy's complete template
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");

        using var invalidKindResponse = await client.GetAsync(
            $"{vacancyLocation}/email-templates/sources?kind=other");
        Assert.Equal(HttpStatusCode.BadRequest, invalidKindResponse.StatusCode);
        await AssertValidationProblemContainsAsync(invalidKindResponse, "kind");

        using var missingVacancyResponse = await client.GetAsync(
            "/api/vacancies/999999/email-templates/sources?kind=shortlisted");
        Assert.Equal(HttpStatusCode.NotFound, missingVacancyResponse.StatusCode);
    }

    [Fact]
    public async Task Template_rendering_resolves_placeholders_and_fallback_names() // US-19: prepared messages personalize candidate and vacancy data without sending mail
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, roundId) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");
        var senderCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            roundId,
            "Alice Applicant",
            "alice@example.com");

        var senderResponse = await RenderAsync(
            client,
            vacancyLocation,
            senderCandidate.Id,
            "Hello {{ Candidate_Name }}",
            "{{candidate_name}} is a fit for {{ vacancy_title }}. {{unknown_token}}");
        Assert.Equal(HttpStatusCode.OK, senderResponse.StatusCode);
        var senderRendered = await senderResponse.Content.ReadFromJsonAsync<RenderedEmailTemplateResponse>();
        Assert.NotNull(senderRendered);
        Assert.Equal("Hello Alice Applicant", senderRendered.Subject);
        Assert.Equal(
            "Alice Applicant is a fit for Data Analyst. {{unknown_token}}",
            senderRendered.Body);

        using var detailsResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/{senderCandidate.Id}/details",
            new { fullName = "Typed Candidate", contactEmail = "typed@example.com" });
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var typedResponse = await RenderAsync(
            client,
            vacancyLocation,
            senderCandidate.Id,
            "Subject {{candidate_name}}",
            "Body {{candidate_name}}");
        var typedRendered = await typedResponse.Content.ReadFromJsonAsync<RenderedEmailTemplateResponse>();
        Assert.NotNull(typedRendered);
        Assert.Equal("Subject Typed Candidate", typedRendered.Subject);
        Assert.Equal("Body Typed Candidate", typedRendered.Body);

        var anonymousCandidate = await ImportCandidateAsync(
            client,
            vacancyLocation,
            roundId,
            senderName: null,
            senderEmail: "anonymous@example.com");
        var anonymousResponse = await RenderAsync(
            client,
            vacancyLocation,
            anonymousCandidate.Id,
            "Subject",
            "Hello {{candidate_name}}");
        var anonymousRendered = await anonymousResponse.Content.ReadFromJsonAsync<RenderedEmailTemplateResponse>();
        Assert.NotNull(anonymousRendered);
        Assert.Equal("Hello there", anonymousRendered.Body);
    }

    [Fact]
    public async Task Template_rendering_rejects_a_candidate_from_another_vacancy() // domain: a candidate belongs to one vacancy through its intake round
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");
        var (otherVacancyLocation, otherRoundId) = await CreateVacancyAsync(
            client,
            "Warehouse Coordinator",
            "2026-08-21");
        var candidate = await ImportCandidateAsync(
            client,
            otherVacancyLocation,
            otherRoundId,
            "Other Applicant",
            "other@example.com");

        using var response = await RenderAsync(
            client,
            vacancyLocation,
            candidate.Id,
            "Subject",
            "Body");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Template_rendering_validates_subject_and_body()
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");

        using var overlongSubjectResponse = await RenderAsync(
            client,
            vacancyLocation,
            candidateId: 1,
            new string('x', 999),
            "Body");
        Assert.Equal(HttpStatusCode.BadRequest, overlongSubjectResponse.StatusCode);
        await AssertValidationProblemContainsAsync(overlongSubjectResponse, "subject");

        using var blankBodyResponse = await RenderAsync(
            client,
            vacancyLocation,
            candidateId: 1,
            "Subject",
            "   ");
        Assert.Equal(HttpStatusCode.BadRequest, blankBodyResponse.StatusCode);
        await AssertValidationProblemContainsAsync(blankBodyResponse, "body");
    }

    [Fact]
    public async Task Closed_vacancy_allows_viewing_but_rejects_template_mutations() // domain: closed vacancy is read-only while retained for reference
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");
        await PutTemplateAsync(client, vacancyLocation, "Existing subject");
        using var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var viewed = await client.GetFromJsonAsync<IReadOnlyList<EmailTemplateResponse>>(
            $"{vacancyLocation}/email-templates");
        Assert.NotNull(viewed);
        Assert.Single(viewed);

        using var upsertResponse = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/rejected",
            new { subject = "New subject", body = "New body" });
        Assert.Equal(HttpStatusCode.Conflict, upsertResponse.StatusCode);
        await AssertProblemAsync(upsertResponse, "EmailTemplates.VacancyClosed");

        using var deleteResponse = await client.DeleteAsync(
            $"{vacancyLocation}/email-templates/shortlisted");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        await AssertProblemAsync(deleteResponse, "EmailTemplates.VacancyClosed");
    }

    [Fact]
    public async Task Deleting_a_missing_template_returns_not_found() // US-19: delete acts on the selected vacancy-owned template
    {
        using var client = factory.CreateClient();
        var (vacancyLocation, _) = await CreateVacancyAsync(client, "Data Analyst", "2026-08-20");

        using var response = await client.DeleteAsync(
            $"{vacancyLocation}/email-templates/rejected");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(string Location, long RoundId)> CreateVacancyAsync(
        HttpClient client,
        string title,
        string openedOn)
    {
        using var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title,
            openedOn,
            requirements = new[] { "SQL" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        var vacancy = await response.Content.ReadFromJsonAsync<VacancyResponse>();
        Assert.NotNull(vacancy);
        return (location, Assert.Single(vacancy.Rounds).Id);
    }

    private static async Task PutTemplateAsync(
        HttpClient client,
        string vacancyLocation,
        string subject)
    {
        using var response = await client.PutAsJsonAsync(
            $"{vacancyLocation}/email-templates/shortlisted",
            new { subject, body = "Template body." });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> RenderAsync(
        HttpClient client,
        string vacancyLocation,
        long candidateId,
        string subject,
        string body) => await client.PostAsJsonAsync(
            $"{vacancyLocation}/email-templates/render",
            new { subject, body, candidateId });

    private static async Task<ImportedCandidate> ImportCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        long roundId,
        string? senderName,
        string senderEmail)
    {
        using var form = new MultipartFormDataContent();
        var source = CreateEml(senderName, senderEmail);
        var fileContent = new ByteArrayContent(source);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", $"{senderEmail}.eml");
        using var response = await client.PostAsync(
            $"{vacancyLocation}/rounds/{roundId}/candidates/import",
            form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(candidate);
        return candidate;
    }

    private static byte[] CreateEml(string? senderName, string senderEmail)
    {
        const string boundary = "hr-sat-email-template-boundary";
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF");
        var sender = senderName is null
            ? $"<{senderEmail}>"
            : $"{senderName} <{senderEmail}>";
        var builder = new StringBuilder();
        builder.Append($"From: {sender}\r\n");
        builder.Append("To: hr@example.com\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append("Subject: Application\r\n");
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

    private static async Task AssertValidationProblemContainsAsync(
        HttpResponseMessage response,
        string key)
    {
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains(problem.Errors.Keys, item =>
            string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
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

    private sealed record VacancyResponse(IReadOnlyList<VacancyRoundResponse> Rounds);

    private sealed record VacancyRoundResponse(long Id);

    private sealed record EmailTemplateResponse(
        long Id,
        long VacancyId,
        string Kind,
        string Subject,
        string Body);

    private sealed record EmailTemplateSourceResponse(
        long VacancyId,
        string VacancyTitle,
        DateOnly OpenedOn,
        string Kind,
        string Subject,
        string Body);

    private sealed record RenderedEmailTemplateResponse(string Subject, string Body);

    private sealed record ImportResponse(IReadOnlyList<ImportResult> Results);

    private sealed record ImportResult(string Status, ImportedCandidate? Candidate);

    private sealed record ImportedCandidate(
        long Id,
        string? SourceSenderName,
        string? SourceSenderEmail);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record ProblemResponse(string? Title, int? Status, string? Detail);
}