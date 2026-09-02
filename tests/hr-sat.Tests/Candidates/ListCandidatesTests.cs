using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace hr_sat.Tests;

public sealed class ListCandidatesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task US_14_HR_sees_an_empty_candidate_list_before_any_email_is_imported()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);

        var response = await client.GetAsync($"{vacancyLocation}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = await response.Content.ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(candidates);
        Assert.Empty(candidates);
    }

    [Fact]
    public async Task US_14_HR_receives_not_found_when_listing_an_unknown_vacancy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vacancies/999/candidates");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task US_14_HR_sees_imported_candidate_summaries_in_import_order()
    {
        using var client = factory.CreateClient();
        var vacancyLocation = await CreateVacancyAsync(client);
        using var form = new MultipartFormDataContent();
        AddFile(
            form,
            CreateEml(
                "Alice Applicant",
                "alice@example.com",
                "Alice application",
                ("alice.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nAlice\n%%EOF"))),
            "alice.eml");
        AddFile(
            form,
            CreateEml(
                "Bob Applicant",
                "bob@example.com",
                "Bob application",
                ("bob.pdf", Encoding.ASCII.GetBytes("%PDF-1.7\nBob\n%%EOF"))),
            "bob.eml");

        var importResponse = await client.PostAsync($"{vacancyLocation}/candidates/import", form);
        importResponse.EnsureSuccessStatusCode();

        var response = await client.GetAsync($"{vacancyLocation}/candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidates = await response.Content.ReadFromJsonAsync<IReadOnlyList<CandidateSummary>>();
        Assert.NotNull(candidates);
        Assert.Collection(
            candidates,
            alice =>
            {
                Assert.True(alice.Id > 0);
                Assert.Null(alice.FullName);
                Assert.Null(alice.ContactEmail);
                Assert.Null(alice.Notes);
                Assert.Equal("new", alice.ReviewStatus);
                Assert.Equal("failed", alice.ExtractionStatus);
                Assert.Equal("Alice Applicant", alice.SourceSenderName);
                Assert.Equal("alice@example.com", alice.SourceSenderEmail);
                Assert.Equal("Alice application", alice.SourceSubject);
                Assert.Equal(
                    new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero),
                    alice.SourceSentAt);
            },
            bob =>
            {
                Assert.True(bob.Id > 0);
                Assert.Null(bob.FullName);
                Assert.Null(bob.ContactEmail);
                Assert.Null(bob.Notes);
                Assert.Equal("new", bob.ReviewStatus);
                Assert.Equal("failed", bob.ExtractionStatus);
                Assert.Equal("Bob Applicant", bob.SourceSenderName);
                Assert.Equal("bob@example.com", bob.SourceSenderEmail);
                Assert.Equal("Bob application", bob.SourceSubject);
                Assert.Equal(
                    new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero),
                    bob.SourceSentAt);
            });
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

    private sealed record CandidateSummary(
        long Id,
        string? FullName,
        string? ContactEmail,
        string? Notes,
        string ReviewStatus,
        string ExtractionStatus,
        string? SourceSenderName,
        string? SourceSenderEmail,
        string? SourceSubject,
        DateTimeOffset? SourceSentAt);
}