using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class ImportCandidatesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Importing_eml_files_preserves_source_data_and_pdf_documents() // US-12/US-13: HR imports emails and retains their source content and PDF attachments
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var alicePdf = Encoding.ASCII.GetBytes("%PDF-1.7\nAlice CV\n%%EOF");
        var bobPdf = Encoding.ASCII.GetBytes("%PDF-1.7\nBob CV\n%%EOF");
        var coverLetterPdf = Encoding.ASCII.GetBytes("%PDF-1.7\nCover letter\n%%EOF");
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Alice Applicant",
                "alice@example.com",
                "Alice application",
                "Please find my CV attached.",
                ("alice.pdf", alicePdf)),
            "alice.eml");
        AddFile(
            form,
            CreateEml(
                "Bob Applicant",
                "bob@example.com",
                "Bob application",
                "Attached are my CV and cover letter.",
                ("bob.pdf", bobPdf),
                ("cover-letter.pdf", coverLetterPdf)),
            "bob.eml");

        var importResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var import = await importResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.Equal(new[] { "imported", "imported" }, import.Results.Select(result => result.Status));

        var alice = import.Results[0].Candidate;
        Assert.NotNull(alice);
        Assert.Equal("Alice Applicant", alice.SourceSenderName);
        Assert.Equal("alice@example.com", alice.SourceSenderEmail);
        Assert.Equal("Alice application", alice.SourceSubject);
        Assert.Equal("Please find my CV attached.", alice.SourceBodyText);
        var aliceDocument = Assert.Single(alice.Documents);
        Assert.True(aliceDocument.IsPrimary);

        var bob = import.Results[1].Candidate;
        Assert.NotNull(bob);
        Assert.Equal(2, bob.Documents.Count);
        Assert.All(bob.Documents, document => Assert.False(document.IsPrimary));

        using var documentResponse = await client.GetAsync(aliceDocument.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/pdf", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(alicePdf, await documentResponse.Content.ReadAsByteArrayAsync());

        var vacancy = await client.GetFromJsonAsync<VacancyResponse>(vacancyLocation);
        Assert.NotNull(vacancy);
        Assert.Equal(0, vacancy.Progress.ProcessedCandidates);
        Assert.Equal(2, vacancy.Progress.TotalCandidates);

        var listedVacancies = await client.GetFromJsonAsync<IReadOnlyList<VacancySummary>>("/api/vacancies");
        Assert.NotNull(listedVacancies);
        var listedVacancy = Assert.Single(listedVacancies);
        Assert.Equal(0, listedVacancy.Progress.ProcessedCandidates);
        Assert.Equal(2, listedVacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Importing_a_batch_keeps_valid_files_when_another_file_fails() // US-12/US-13: each dropped email is processed independently
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Valid Applicant",
                "valid@example.com",
                "Valid application",
                "A valid application.",
                ("valid.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nValid\n%%EOF"))),
            "valid.eml");
        AddFile(
            form,
            CreateEml(
                "Missing CV Applicant",
                "missing@example.com",
                "Missing CV",
                "There is no PDF here."),
            "missing.eml");

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        Assert.Equal("imported", import.Results[0].Status);
        Assert.Equal("failed", import.Results[1].Status);
        Assert.Contains("at least one valid PDF", import.Results[1].Error, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(import.Results[0].Candidate);
        Assert.Null(import.Results[1].Candidate);

        var vacancy = await client.GetFromJsonAsync<VacancyResponse>(vacancyLocation);
        Assert.NotNull(vacancy);
        Assert.Equal(1, vacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Importing_the_same_source_email_twice_skips_the_duplicate() // domain: exact source bytes are unique within a vacancy
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var source = CreateEml(
            "Duplicate Applicant",
            "duplicate@example.com",
            "Duplicate application",
            "The same bytes are submitted twice.",
            ("duplicate.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nDuplicate\n%%EOF")));

        using (var firstForm = new MultipartFormDataContent())
        {
            AddFile(firstForm, source, "first.eml");
            var firstResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", firstForm);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        }

        using var secondForm = new MultipartFormDataContent();
        AddFile(secondForm, source, "second.eml");
        var secondResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", secondForm);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var import = await secondResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var result = Assert.Single(import.Results);
        Assert.Equal("skipped", result.Status);
        Assert.Null(result.Candidate);

        var vacancy = await client.GetFromJsonAsync<VacancyResponse>(vacancyLocation);
        Assert.NotNull(vacancy);
        Assert.Equal(1, vacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Importing_into_a_closed_vacancy_is_rejected() // domain: closed vacancy is read-only and cannot receive candidate imports
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Closed Applicant",
                "closed@example.com",
                "Closed vacancy application",
                "This should not be imported.",
                ("closed.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nClosed\n%%EOF"))),
            "closed.eml");

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("status", problem.Errors.Keys);
        var vacancy = await client.GetFromJsonAsync<VacancyResponse>(vacancyLocation);
        Assert.NotNull(vacancy);
        Assert.Equal(0, vacancy.Progress.TotalCandidates);
    }

    [Fact]
    public async Task Importing_without_files_returns_a_validation_problem() // US-12: the drop zone must receive at least one exported .eml file
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        using var form = new MultipartFormDataContent();

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("files", problem.Errors.Keys);
    }

    private static async Task<string> CreateVacancyAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/vacancies", new
        {
            title = "Data Analyst",
            openedOn = "2026-08-20",
            requirements = new[] { "SQL" }
        });
        response.EnsureSuccessStatusCode();
        return response.Headers.Location!.OriginalString;
    }

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

    private sealed record ImportFileResponse(
        string FileName,
        string Status,
        string? Error,
        CandidateResponse? Candidate);

    private sealed record CandidateResponse(
        long Id,
        string ReviewStatus,
        string ExtractionStatus,
        string? SourceSenderName,
        string? SourceSenderEmail,
        string? SourceSubject,
        string? SourceBodyText,
        IReadOnlyList<CvDocumentResponse> Documents);

    private sealed record CvDocumentResponse(
        long Id,
        string OriginalFilename,
        long SizeBytes,
        bool IsPrimary,
        string DownloadUrl);

    private sealed record VacancyResponse(
        long Id,
        VacancyProgress Progress);

    private sealed record VacancySummary(
        long Id,
        VacancyProgress Progress);

    private sealed record VacancyProgress(int ProcessedCandidates, int TotalCandidates);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}