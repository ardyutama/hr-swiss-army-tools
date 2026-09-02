using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests;

public sealed class CandidateExtractionFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task US_18_HR_can_extract_candidate_details_and_exact_requirement_matches_from_a_text_layer_pdf()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL", "C#", "Python");
        var pdf = CreateTextPdf(
            "Name: Ada Example",
            "Email: ada@example.test",
            "Phone: +1 555 123 4567",
            "Skills: SQL, C#, SQL Server");
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Source Sender",
                "sender@example.test",
                "Text CV",
                pdf),
            "ada.eml");

        var importResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var import = await importResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(candidate);
        Assert.Equal("succeeded", candidate.ExtractionStatus);
        Assert.Equal("Ada Example", candidate.FullName);
        Assert.Equal("ada@example.test", candidate.ContactEmail);
        Assert.Equal("+1 555 123 4567", candidate.ContactPhone);
        Assert.Equal(new[] { "SQL", "C#", "SQL Server" }, candidate.Skills.Select(skill => skill.Phrase));
        Assert.Equal(2, candidate.Match.MatchedRequirements);
        Assert.Equal(3, candidate.Match.TotalRequirements);
        Assert.Equal("new", candidate.ReviewStatus);

        var detailResponse = await client.GetAsync(
            $"{vacancyLocation}/candidates/{candidate.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(detail);
        Assert.Equal("succeeded", detail.ExtractionStatus);
        Assert.Equal("Ada Example", detail.FullName);
        Assert.Equal("ada@example.test", detail.ContactEmail);
        Assert.Equal("+1 555 123 4567", detail.ContactPhone);
        Assert.Equal(new[] { "SQL", "C#", "SQL Server" }, detail.Skills.Select(skill => skill.Phrase));
        Assert.Equal(2, detail.Match.MatchedRequirements);
        Assert.Equal(3, detail.Match.TotalRequirements);
        Assert.Equal("new", detail.ReviewStatus);
        Assert.Single(detail.Documents);
        Assert.Equal(candidate.Documents[0].Id, detail.Documents[0].Id);
        Assert.True(detail.Documents[0].IsPrimary);

        var listedCandidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/candidates");
        Assert.NotNull(listedCandidates);
        var listedCandidate = Assert.Single(listedCandidates);
        Assert.Equal("Ada Example", listedCandidate.FullName);
        Assert.Equal("ada@example.test", listedCandidate.ContactEmail);
        Assert.Equal("succeeded", listedCandidate.ExtractionStatus);
        Assert.Equal(new[] { "SQL", "C#", "SQL Server" }, listedCandidate.Skills.Select(skill => skill.Phrase));
        Assert.Equal(2, listedCandidate.Match.MatchedRequirements);
        Assert.Equal(3, listedCandidate.Match.TotalRequirements);
        Assert.Equal("new", listedCandidate.ReviewStatus);
    }

    [Fact]
    public async Task US_18_HR_retains_a_candidate_and_its_document_when_the_pdf_has_no_text_layer()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var pdf = CreateBlankPdf();
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Unreadable Sender",
                "unreadable@example.test",
                "Scanned CV",
                pdf),
            "unreadable.eml");

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(candidate);
        Assert.Equal("failed", candidate.ExtractionStatus);
        Assert.Null(candidate.FullName);
        var document = Assert.Single(candidate.Documents);

        using var documentResponse = await client.GetAsync(document.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal(pdf, await documentResponse.Content.ReadAsByteArrayAsync());

        var candidates = await client.GetFromJsonAsync<IReadOnlyList<CandidateSummary>>(
            $"{vacancyLocation}/candidates");
        Assert.NotNull(candidates);
        Assert.Single(candidates);
        Assert.Equal("failed", candidates[0].ExtractionStatus);
    }

    [Fact]
    public async Task US_18_HR_sees_pending_extraction_when_an_import_has_no_primary_cv()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var firstPdf = CreateTextPdf("Name: First Applicant", "Skills: Python");
        var secondPdf = CreateTextPdf("Name: Second Applicant", "Skills: SQL");
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Multiple Sender",
                "multiple@example.test",
                "Multiple CVs",
                ("first.pdf", firstPdf),
                ("second.pdf", secondPdf)),
            "multiple.eml");

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var import = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(candidate);
        Assert.Equal("pending", candidate.ExtractionStatus);
        Assert.Null(candidate.FullName);
        Assert.Empty(candidate.Skills);
        Assert.Equal(2, candidate.Documents.Count);
        Assert.All(candidate.Documents, document => Assert.False(document.IsPrimary));
    }

    [Fact]
    public async Task US_18_HR_selects_a_primary_cv_then_retries_extraction_using_only_that_document()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL", "Docker");
        var firstPdf = CreateTextPdf(
            "Name: First Applicant",
            "Email: first@example.test",
            "Skills: Python");
        var secondPdf = CreateTextPdf(
            "Name: Second Applicant",
            "Email: second@example.test",
            "Skills: SQL, Docker");
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Multiple Sender",
                "multiple@example.test",
                "Multiple CVs",
                ("first.pdf", firstPdf),
                ("second.pdf", secondPdf)),
            "multiple.eml");

        var importResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", form);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var import = await importResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var importedCandidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(importedCandidate);
        var requestedDocument = importedCandidate.Documents[1];

        var selectResponse = await client.PutAsync(
            $"{requestedDocument.DownloadUrl}/primary",
            content: null);

        Assert.Equal(HttpStatusCode.OK, selectResponse.StatusCode);
        var selectedCandidate = await selectResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(selectedCandidate);
        Assert.Equal("pending", selectedCandidate.ExtractionStatus);
        Assert.False(selectedCandidate.Documents[0].IsPrimary);
        Assert.True(selectedCandidate.Documents[1].IsPrimary);
        Assert.Equal("new", selectedCandidate.ReviewStatus);

        var extractResponse = await client.PostAsync(
            $"{vacancyLocation}/candidates/{importedCandidate.Id}/extract",
            content: null);

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);
        var extractedCandidate = await extractResponse.Content.ReadFromJsonAsync<CandidateResponse>();
        Assert.NotNull(extractedCandidate);
        Assert.Equal("succeeded", extractedCandidate.ExtractionStatus);
        Assert.Equal("Second Applicant", extractedCandidate.FullName);
        Assert.Equal("second@example.test", extractedCandidate.ContactEmail);
        Assert.Equal(new[] { "SQL", "Docker" }, extractedCandidate.Skills.Select(skill => skill.Phrase));
        Assert.Equal(2, extractedCandidate.Match.MatchedRequirements);
        Assert.Equal(2, extractedCandidate.Match.TotalRequirements);
        Assert.Equal("new", extractedCandidate.ReviewStatus);
    }

    [Fact]
    public async Task Extracting_a_candidate_in_a_closed_vacancy_is_rejected() // domain: a Closed Vacancy is read-only
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client, "SQL");
        var pdf = CreateTextPdf("Name: Closed Applicant", "Skills: SQL");
        using var importForm = new MultipartFormDataContent();
        AddFile(
            importForm,
            CreateEml(
                "Closed Sender",
                "closed@example.test",
                "Closed CV",
                pdf),
            "closed.eml");
        var importResponse = await client.PostAsync(
            $"{vacancyLocation}/candidates/import",
            importForm);
        var import = await importResponse.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(import);
        var candidate = Assert.Single(import.Results).Candidate;
        Assert.NotNull(candidate);
        var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsync(
            $"{vacancyLocation}/candidates/{candidate.Id}/extract",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private static void AddFile(
        MultipartFormDataContent form,
        byte[] content,
        string filename)
    {
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(fileContent, "files", filename);
    }

    private static byte[] CreateEml(
        string senderName,
        string senderEmail,
        string subject,
        byte[] attachment) =>
        CreateEml(
            senderName,
            senderEmail,
            subject,
            ("candidate.pdf", attachment));

    private static byte[] CreateEml(
        string senderName,
        string senderEmail,
        string subject,
        params (string Filename, byte[] Content)[] attachments)
    {
        const string boundary = "hr-sat-extraction-boundary";
        var builder = new StringBuilder();
        builder.Append($"From: {senderName} <{senderEmail}>\r\n");
        builder.Append("To: hr@example.test\r\n");
        builder.Append("Date: Sat, 29 Aug 2026 10:00:00 +0000\r\n");
        builder.Append($"Subject: {subject}\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append($"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n\r\n");
        builder.Append("Please find my CV attached.\r\n");

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

    private static byte[] CreateTextPdf(params string[] lines)
    {
        var content = new StringBuilder("BT\n/F1 12 Tf\n72 720 Td\n");
        foreach (var line in lines)
        {
            content.Append('(')
                .Append(EscapePdfText(line))
                .Append(") Tj\n0 -18 Td\n");
        }

        content.Append("ET");
        return CreatePdf(content.ToString());
    }

    private static byte[] CreateBlankPdf() => CreatePdf("q\nQ");

    private static byte[] CreatePdf(string content)
    {
        var contentLength = Encoding.ASCII.GetByteCount(content);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {contentLength} >>\nstream\n{content}\nendstream"
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };

        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Length + 1}\n");
        builder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }

        builder.Append(
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EscapePdfText(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

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
        string? FullName,
        string? ContactEmail,
        string? ContactPhone,
        IReadOnlyList<CandidateSkillResponse> Skills,
        CandidateMatchResponse Match,
        IReadOnlyList<CvDocumentResponse> Documents);

    private sealed record CandidateSkillResponse(string Phrase, int Position);

    private sealed record CandidateMatchResponse(int MatchedRequirements, int TotalRequirements);

    private sealed record CvDocumentResponse(
        long Id,
        string OriginalFilename,
        long SizeBytes,
        bool IsPrimary,
        string DownloadUrl);

    private sealed record CandidateSummary(
        long Id,
        string? FullName,
        string? ContactEmail,
        string? ContactPhone,
        string? Notes,
        string ReviewStatus,
        string ExtractionStatus,
        IReadOnlyList<CandidateSkillResponse> Skills,
        CandidateMatchResponse Match);
}
