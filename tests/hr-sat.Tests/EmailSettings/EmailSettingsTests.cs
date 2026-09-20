using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace hr_sat.Tests.EmailSettings;

public sealed class EmailSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // The ApiFactory installs a complete Smtp file section (host smtp.test.invalid),
    // so the configuration-file fallback is the initial effective source.

    [Fact]
    public async Task Smtp_settings_roundtrip_through_save_and_remove() // domain: SMTP Account — a complete saved row wins; the file is the fallback; removal restores it
    {
        using var client = factory.CreateClient();

        // First open: prefilled from the configuration file, password withheld.
        var initial = await GetSettingsAsync(client);
        Assert.Equal("configuration-file", initial.Source);
        Assert.Equal("smtp.test.invalid", initial.Host);
        Assert.Equal(587, initial.Port);
        Assert.True(initial.HasPassword);

        using var saveResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = "unit-secret",
            fromAddress = "hr@firma.example",
            fromName = "HR Team",
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var savedBody = await saveResponse.Content.ReadAsStringAsync();
        // The password is write-only: it never crosses the wire back (decision 6).
        Assert.DoesNotContain("unit-secret", savedBody);
        Assert.DoesNotContain("file-password", savedBody);
        var saved = JsonDocument.Parse(savedBody).RootElement;
        Assert.Equal("settings", saved.GetProperty("source").GetString());
        Assert.True(saved.GetProperty("hasPassword").GetBoolean());
        Assert.Equal("smtp.gmail.com", saved.GetProperty("host").GetString());
        Assert.Equal("HR Team", saved.GetProperty("fromName").GetString());

        var afterSave = await GetSettingsAsync(client);
        Assert.Equal("settings", afterSave.Source);
        Assert.Equal("hr@firma.example", afterSave.Username);

        // Omitting the password keeps the stored one (decision 12).
        using var keepResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            host = "smtp.office365.com",
            port = 587,
            username = "hr@firma.example",
            password = (string?)null,
            fromAddress = "hr@firma.example",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, keepResponse.StatusCode);
        var kept = await GetSettingsAsync(client);
        Assert.Equal("settings", kept.Source);
        Assert.Equal("smtp.office365.com", kept.Host);
        Assert.True(kept.HasPassword);
        Assert.Null(kept.FromName);

        using var removeResponse = await client.DeleteAsync("/api/settings/smtp");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        var afterRemove = await GetSettingsAsync(client);
        Assert.Equal("configuration-file", afterRemove.Source);
        Assert.Equal("smtp.test.invalid", afterRemove.Host);
    }

    [Fact]
    public async Task Smtp_settings_saved_without_a_password_fall_back_to_the_file() // domain: SMTP Account — an incomplete row never takes effect (decision 2)
    {
        using var client = factory.CreateClient();

        using var saveResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = (string?)null,
            fromAddress = "hr@firma.example",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var settings = await GetSettingsAsync(client);
        Assert.Equal("configuration-file", settings.Source);
        Assert.Equal("smtp.test.invalid", settings.Host);
    }

    [Fact]
    public async Task Smtp_settings_validate_the_input_per_field() // domain: SMTP Account — the server mirrors and owns the input rules
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            host = "https://smtp.gmail.com",
            port = 70000,
            username = " ",
            password = "app-password",
            fromAddress = "not-an-email",
            fromName = new string('x', 201),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        foreach (var key in new[] { "host", "port", "username", "fromAddress", "fromName" })
        {
            Assert.Contains(problem.Errors.Keys, item =>
                string.Equals(item, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Smtp_test_connection_reports_a_sanitized_failure_and_persists_nothing() // domain: SMTP Account — the handshake must pass; raw exception text never crosses the wire
    {
        using var client = factory.CreateClient();

        using var testResponse = await client.PostAsJsonAsync("/api/settings/smtp/test", new
        {
            host = "localhost",
            port = 1, // nothing listens here: the connect stage fails fast
            username = "hr@firma.example",
            password = "unit-secret",
        });

        Assert.Equal(HttpStatusCode.BadRequest, testResponse.StatusCode);
        var problem = await testResponse.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("EmailSettings.TestFailed", problem.Title);
        Assert.Equal("Could not connect to localhost:1.", problem.Detail);

        // The probe persists nothing (decision 3).
        var settings = await GetSettingsAsync(client);
        Assert.Equal("configuration-file", settings.Source);
    }

    [Fact]
    public async Task Smtp_test_connection_uses_the_stored_password_when_the_field_is_untouched() // domain: SMTP Account — omitted password keeps the stored one
    {
        using var client = factory.CreateClient();

        // Nothing stored yet: the probe refuses on the password field.
        using var refused = await client.PostAsJsonAsync("/api/settings/smtp/test", new
        {
            host = "localhost",
            port = 1,
            username = "hr@firma.example",
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var refusedProblem = await refused.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(refusedProblem);
        Assert.Contains(refusedProblem.Errors.Keys, item =>
            string.Equals(item, "password", StringComparison.OrdinalIgnoreCase));

        // Once a password is saved, an untouched field tests against the stored one —
        // reaching the connect stage proves the stored password was read and unprotected.
        using var saveResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = "unit-secret",
            fromAddress = "hr@firma.example",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        using var retest = await client.PostAsJsonAsync("/api/settings/smtp/test", new
        {
            host = "localhost",
            port = 1,
            username = "hr@firma.example",
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, retest.StatusCode);
        var retestProblem = await retest.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(retestProblem);
        Assert.Equal("EmailSettings.TestFailed", retestProblem.Title);
        Assert.Equal("Could not connect to localhost:1.", retestProblem.Detail);
    }

    private static async Task<SmtpSettingsContract> GetSettingsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/settings/smtp");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // The password never leaves the server — not the file's, not the saved row's.
        Assert.DoesNotContain("file-password", body);
        Assert.DoesNotContain("unit-secret", body);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.False(json.TryGetProperty("password", out _));
        return new SmtpSettingsContract(
            json.GetProperty("source").GetString()!,
            json.GetProperty("host").GetString(),
            json.GetProperty("port").GetInt32OrNull(),
            json.GetProperty("username").GetString(),
            json.GetProperty("fromName").GetString(),
            json.GetProperty("hasPassword").GetBoolean());
    }

    private sealed record SmtpSettingsContract(
        string Source,
        string? Host,
        int? Port,
        string? Username,
        string? FromName,
        bool HasPassword);

    private sealed record ValidationProblemResponse(Dictionary<string, string[]> Errors);

    private sealed record ProblemResponse(string Title, string Detail, int Status);
}

file static class JsonElementExtensions
{
    public static int? GetInt32OrNull(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Number ? element.GetInt32() : null;
}
