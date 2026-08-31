using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Server.Tests.Candidates;

public sealed class DeleteCandidateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task US_14_HR_deletes_a_candidate_and_it_leaves_the_vacancy_pipeline()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var imported = await ImportAsync(client, vacancyLocation, ("alice.eml", "Alice Applicant", "alice@example.com"), ("bob.eml", "Bob Applicant", "bob@example.com"));
        var bob = imported.Results.Single(result => result.FileName == "bob.eml").Candidate!;
        var bobDocumentUrl = bob.Documents.Single().DownloadUrl;

        var deleteResponse = await client.DeleteAsync($"{vacancyLocation}/candidates/{bob.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // The vacancy pipeline no longer shows Bob.
        var candidates = await (await client.GetAsync($"{vacancyLocation}/candidates"))
            .Content.ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(candidates);
        var remaining = Assert.Single(candidates);
        Assert.Equal("Alice Applicant", remaining.SourceSenderName);

        // Bob's CV document is gone as well.
        var documentResponse = await client.GetAsync(bobDocumentUrl);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }

    [Fact]
    public async Task US_14_deleting_an_unknown_candidate_returns_not_found()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);

        var response = await client.DeleteAsync($"{vacancyLocation}/candidates/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var missingVacancyResponse = await client.DeleteAsync("/api/vacancies/999/candidates/1");
        Assert.Equal(HttpStatusCode.NotFound, missingVacancyResponse.StatusCode);
    }

    // domain: closed vacancy is read-only
    [Fact]
    public async Task Closed_vacancy_rejects_candidate_removal()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        var imported = await ImportAsync(client, vacancyLocation, ("alice.eml", "Alice Applicant", "alice@example.com"));
        var aliceId = imported.Results.Single().Candidate!.Id;
        var closeResponse = await client.PostAsync($"{vacancyLocation}/close", content: null);
        closeResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"{vacancyLocation}/candidates/{aliceId}");

        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        var problem = await deleteResponse.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.Contains("status", problem.Errors.Keys);

        // The candidate is retained for reference.
        var candidates = await (await client.GetAsync($"{vacancyLocation}/candidates"))
            .Content.ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(candidates);
        Assert.Single(candidates);
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

    private static async Task<ImportResponse> ImportAsync(
        HttpClient client,
        string vacancyLocation,
        params (string FileName, string SenderName, string SenderEmail)[] files)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (fileName, senderName, senderEmail) in files)
        {
            AddFile(
                form,
                CreateEml(
                    senderName,
                    senderEmail,
                    $"{senderName} application",
                    ($"{senderName}.pdf", Encoding.ASCII.GetBytes($"%PDF-1.7\n{senderName}\n%%EOF"))),
                fileName);
        }

        var response = await client.PostAsync($"{vacancyLocation}/candidates/import", form);
        response.EnsureSuccessStatusCode();
        var imported = await response.Content.ReadFromJsonAsync<ImportResponse>();
        Assert.NotNull(imported);
        return imported;
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
        (string Filename, byte[] Content) attachment)
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
        builder.Append("Please find my CV attached.\r\n");
        builder.Append($"--{boundary}\r\n");
        builder.Append($"Content-Type: application/pdf; name=\"{attachment.Filename}\"\r\n");
        builder.Append($"Content-Disposition: attachment; filename=\"{attachment.Filename}\"\r\n");
        builder.Append("Content-Transfer-Encoding: base64\r\n");
        builder.Append("\r\n");
        builder.Append(Convert.ToBase64String(attachment.Content));
        builder.Append("\r\n");
        builder.Append($"--{boundary}--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private sealed record ImportResponse(IReadOnlyList<ImportFileResponse> Results);

    private sealed record ImportFileResponse(
        string FileName,
        string Status,
        string? Error,
        CandidateResponse? Candidate);

    private sealed record CandidateResponse(long Id, IReadOnlyList<CvDocumentResponse> Documents);

    private sealed record CvDocumentResponse(long Id, string DownloadUrl);

    private sealed record CandidateSummary(long Id, string? SourceSenderName);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);
}
