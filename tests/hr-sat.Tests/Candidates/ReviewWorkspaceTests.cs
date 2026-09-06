using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests;

public sealed class ReviewWorkspaceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Candidate_details_validate_and_persist_name_and_email()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var candidate = await ImportCandidateAsync(client, vacancyLocation, "Alice Applicant");

        var invalidResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "details"),
            new { fullName = "", contactEmail = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var problem = await invalidResponse.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("fullName", problem.Errors.Keys);
        Assert.Contains("contactEmail", problem.Errors.Keys);

        var updateResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "details"),
            new { fullName = "Jane Updated", contactEmail = "jane.updated@example.com" });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<CandidateDetails>();
        Assert.NotNull(updated);
        Assert.Equal("Jane Updated", updated.FullName);
        Assert.Equal("jane.updated@example.com", updated.ContactEmail);

        var persisted = await client.GetFromJsonAsync<CandidateDetails>(
            CandidatePath(vacancyLocation, candidate.Id));
        Assert.NotNull(persisted);
        Assert.Equal("Jane Updated", persisted.FullName);
        Assert.Equal("jane.updated@example.com", persisted.ContactEmail);
    }

    [Fact]
    public async Task Candidate_notes_persist_through_the_review_endpoint()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var candidate = await ImportCandidateAsync(client, vacancyLocation, "Alice Applicant");

        var response = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "notes"),
            new { notes = "Call back about the reporting experience." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CandidateDetails>();
        Assert.NotNull(updated);
        Assert.Equal("Call back about the reporting experience.", updated.Notes);

        var persisted = await client.GetFromJsonAsync<CandidateDetails>(
            CandidatePath(vacancyLocation, candidate.Id));
        Assert.NotNull(persisted);
        Assert.Equal("Call back about the reporting experience.", persisted.Notes);
    }

    [Fact]
    public async Task Review_decision_persists_status_and_pending_notes_in_one_commit()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var candidate = await ImportCandidateAsync(client, vacancyLocation, "Alice Applicant");

        var response = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "review"),
            new { reviewStatus = "shortlisted", notes = "Move to interview." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CandidateDetails>();
        Assert.NotNull(updated);
        Assert.Equal("shortlisted", updated.ReviewStatus);
        Assert.Equal("Move to interview.", updated.Notes);

        var listed = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/candidates");
        Assert.NotNull(listed);
        var summary = Assert.Single(listed);
        Assert.Equal("shortlisted", summary.ReviewStatus);
        Assert.Equal("Move to interview.", summary.Notes);
    }

    [Fact]
    public async Task Requirement_review_persists_and_progress_counts_only_shortlisted_or_rejected()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL", "VAT reporting");
        var vacancy = await client.GetFromJsonAsync<VacancyDetails>(vacancyLocation);
        Assert.NotNull(vacancy);
        var requirementId = vacancy.Requirements[1].Id;

        var candidates = await ImportCandidatesAsync(
            client,
            vacancyLocation,
            "Alice Applicant",
            "Bob Applicant",
            "Cara Applicant");

        var requirementResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidates[0].Id, $"requirement-reviews/{requirementId}"),
            new { confirmed = true });

        Assert.Equal(HttpStatusCode.OK, requirementResponse.StatusCode);
        var reviewed = await requirementResponse.Content.ReadFromJsonAsync<CandidateDetails>();
        Assert.NotNull(reviewed);
        var requirementReview = Assert.Single(
            reviewed.RequirementReviews,
            review => review.RequirementId == requirementId);
        Assert.True(requirementReview.Confirmed);

        await UpdateReviewAsync(client, vacancyLocation, candidates[0].Id, "shortlisted");
        await UpdateReviewAsync(client, vacancyLocation, candidates[1].Id, "flagged");
        await UpdateReviewAsync(client, vacancyLocation, candidates[2].Id, "rejected");

        var persisted = await client.GetFromJsonAsync<CandidateDetails>(
            CandidatePath(vacancyLocation, candidates[0].Id));
        Assert.NotNull(persisted);
        Assert.Contains(
            persisted.RequirementReviews,
            review => review.RequirementId == requirementId && review.Confirmed);

        var updatedVacancy = await client.GetFromJsonAsync<VacancyDetails>(vacancyLocation);
        Assert.NotNull(updatedVacancy);
        Assert.Equal(2, updatedVacancy.Progress.ProcessedCandidates);
        Assert.Equal(3, updatedVacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Closed_vacancy_rejects_all_candidate_review_writes()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var vacancy = await client.GetFromJsonAsync<VacancyDetails>(vacancyLocation);
        Assert.NotNull(vacancy);
        var candidate = await ImportCandidateAsync(client, vacancyLocation, "Alice Applicant");
        var requirementId = vacancy.Requirements[0].Id;

        var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var detailsResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "details"),
            new { fullName = "Updated Name", contactEmail = "updated@example.com" });
        var notesResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "notes"),
            new { notes = "Should not save." });
        var reviewResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, "review"),
            new { reviewStatus = "shortlisted", notes = "Should not save." });
        var requirementResponse = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidate.Id, $"requirement-reviews/{requirementId}"),
            new { confirmed = true });

        Assert.Equal(HttpStatusCode.BadRequest, detailsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reviewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, requirementResponse.StatusCode);

        var persisted = await client.GetFromJsonAsync<CandidateDetails>(
            CandidatePath(vacancyLocation, candidate.Id));
        Assert.NotNull(persisted);
        Assert.Equal("new", persisted.ReviewStatus);
        Assert.Null(persisted.FullName);
        Assert.Null(persisted.ContactEmail);
        Assert.Null(persisted.Notes);
        Assert.Empty(persisted.RequirementReviews);
    }

    private static async Task<string> CreateVacancyAsync(
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
        return response.Headers.Location!.OriginalString;
    }

    private static async Task<ImportedCandidate> ImportCandidateAsync(
        HttpClient client,
        string vacancyLocation,
        string senderName)
    {
        var candidates = await ImportCandidatesAsync(client, vacancyLocation, senderName);
        return Assert.Single(candidates);
    }

    private static async Task<IReadOnlyList<ImportedCandidate>> ImportCandidatesAsync(
        HttpClient client,
        string vacancyLocation,
        params string[] senderNames)
    {
        using var form = new MultipartFormDataContent();
        for (var index = 0; index < senderNames.Length; index++)
        {
            var senderName = senderNames[index];
            AddFile(
                form,
                CreateEml(
                    senderName,
                    $"candidate{index + 1}@example.com",
                    $"{senderName} application",
                    "Please review my application.",
                    ("candidate.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nCandidate\n%%EOF"))),
                $"candidate-{index + 1}.eml");
        }

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.All(import.Results, result => Assert.Equal("imported", result.Status));
        return import.Results.Select(result => result.Candidate!).ToList();
    }

    private static async Task UpdateReviewAsync(
        HttpClient client,
        string vacancyLocation,
        long candidateId,
        string reviewStatus)
    {
        var response = await client.PutAsJsonAsync(
            CandidatePath(vacancyLocation, candidateId, "review"),
            new { reviewStatus, notes = "" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string CandidatePath(
        string vacancyLocation,
        long candidateId,
        string? suffix = null) =>
        $"{vacancyLocation}/candidates/{candidateId}{(suffix is null ? string.Empty : $"/{suffix}")}";

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
        builder.Append("Content-Transfer-Encoding: 8bit\r\n");
        builder.Append("\r\n");
        builder.Append(body);
        builder.Append("\r\n");

        foreach (var attachment in attachments)
        {
            builder.Append($"--{boundary}\r\n");
            builder.Append($"Content-Type: application/pdf; name=\"{attachment.Filename}\"\r\n");
            builder.Append($"Content-Disposition: attachment; filename=\"{attachment.Filename}\"\r\n");
            builder.Append("Content-Transfer-Encoding: base64\r\n");
            builder.Append("\r\n");
            builder.Append(Convert.ToBase64String(attachment.Content));
            builder.Append("\r\n");
        }

        builder.Append($"--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private sealed record ImportResponse(IReadOnlyList<ImportFileResponse> Results);

    private sealed record ImportFileResponse(string Status, ImportedCandidate? Candidate);

    private sealed record ImportedCandidate(long Id, IReadOnlyList<CvDocument> Documents);

    private sealed record CvDocument(
        long Id,
        string OriginalFilename,
        long SizeBytes,
        bool IsPrimary,
        string DownloadUrl);

    private sealed record CandidateDetails(
        long Id,
        string ReviewStatus,
        string? FullName,
        string? ContactEmail,
        string? Notes,
        IReadOnlyList<RequirementReview> RequirementReviews,
        string? SourceSenderName,
        string? SourceSenderEmail,
        string? SourceSubject,
        string? SourceBodyText,
        DateTimeOffset? SourceSentAt,
        string SourceOriginalFilename,
        IReadOnlyList<CvDocument> Documents);

    private sealed record RequirementReview(long RequirementId, bool Confirmed);

    private sealed record CandidateSummary(
        long Id,
        string? FullName,
        string? ContactEmail,
        string? Notes,
        string ReviewStatus);

    private sealed record VacancyDetails(
        IReadOnlyList<VacancyRequirement> Requirements,
        VacancyProgress Progress);

    private sealed record VacancyRequirement(long Id, string Phrase, int Position);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}