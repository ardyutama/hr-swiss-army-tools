using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using hr_sat.Application.Abstractions.Email;
using hr_sat.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.EmailSettings;

// Seam tests for the Microsoft Account sign-in (issue 03, decision 14): the connect
// begin/status flow runs against the fake sign-in seam — no live Microsoft calls —
// while the settings rows, grant protection, and readiness orchestration run for real
// over the Testcontainers database.
public sealed class MicrosoftSignInTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Microsoft_connect_begin_returns_the_device_code_challenge() // domain: Sign-in Method — the panel shows the code, the URL, and the expiry (decision 32)
    {
        using var client = factory.CreateClient();

        using var beginResponse = await client.PostAsync(
            "/api/settings/smtp/microsoft-connect/begin",
            content: null);

        Assert.Equal(HttpStatusCode.OK, beginResponse.StatusCode);
        var challenge = await beginResponse.Content.ReadFromJsonAsync<ConnectChallengeContract>();
        Assert.NotNull(challenge);
        Assert.Equal("ABCD-EFGH", challenge.UserCode);
        Assert.Equal("https://microsoft.com/link", challenge.VerificationUrl);
        Assert.True(challenge.ExpiresAt > DateTimeOffset.MinValue);

        // A begun attempt is pending until it settles.
        var status = await client.GetFromJsonAsync<ConnectStatusContract>(
            "/api/settings/smtp/microsoft-connect/status");
        Assert.NotNull(status);
        Assert.Equal("pending", status.State);
    }

    [Fact]
    public async Task Microsoft_connect_begin_is_refused_when_sign_in_is_unavailable() // domain: Sign-in Method — no client ID, no Microsoft choice (decision 24)
    {
        // CreateClient resets the fake; flip availability afterwards.
        using var client = factory.CreateClient();
        factory.MicrosoftSignIn.IsAvailable = false;

        using var beginResponse = await client.PostAsync(
            "/api/settings/smtp/microsoft-connect/begin",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, beginResponse.StatusCode);
        var problem = await beginResponse.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("EmailSettings.MicrosoftSignInUnavailable", problem.Title);
        Assert.Equal(0, factory.MicrosoftSignIn.BeginCallCount);

        // The GET tells the client to disable the choice before the click.
        var settings = await client.GetFromJsonAsync<SettingsContract>("/api/settings/smtp");
        Assert.NotNull(settings);
        Assert.False(settings.MicrosoftSignInAvailable);
    }

    [Fact]
    public async Task Microsoft_connect_status_reports_success_decline_and_expiry() // domain: Sign-in Method — the poll surfaces each terminal outcome (decision 21)
    {
        using var client = factory.CreateClient();

        // Success: the poll reports the connected account — the derived sender
        // identity (decision 5).
        await client.PostAsync("/api/settings/smtp/microsoft-connect/begin", content: null);
        factory.MicrosoftSignIn.SetSucceeded("anna@outlook.com");
        var succeeded = await client.GetFromJsonAsync<ConnectStatusContract>(
            "/api/settings/smtp/microsoft-connect/status");
        Assert.NotNull(succeeded);
        Assert.Equal("succeeded", succeeded.State);
        Assert.Equal("anna@outlook.com", succeeded.AccountEmail);

        // Declined: a fresh begin replaces the terminal state (last-write-wins), then
        // reports the sanitized failure copy.
        await client.PostAsync("/api/settings/smtp/microsoft-connect/begin", content: null);
        factory.MicrosoftSignIn.SetFailed("The sign-in was declined before it finished.");
        var failed = await client.GetFromJsonAsync<ConnectStatusContract>(
            "/api/settings/smtp/microsoft-connect/status");
        Assert.NotNull(failed);
        Assert.Equal("failed", failed.State);
        Assert.Equal("The sign-in was declined before it finished.", failed.Error);

        // Expired: the device code ran out before sign-in finished.
        await client.PostAsync("/api/settings/smtp/microsoft-connect/begin", content: null);
        factory.MicrosoftSignIn.SetExpired();
        var expired = await client.GetFromJsonAsync<ConnectStatusContract>(
            "/api/settings/smtp/microsoft-connect/status");
        Assert.NotNull(expired);
        Assert.Equal("expired", expired.State);
    }

    [Fact]
    public async Task Microsoft_account_roundtrip_connect_save_and_method_switch_discards_credentials() // domain: Sign-in Method — one method at a time; switching discards the other credential (decisions 10, 11)
    {
        using var client = factory.CreateClient();

        // An App Password account first: password stored, method app-password.
        using var saveResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "app-password",
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = "unit-secret",
            fromAddress = "hr@firma.example",
            fromName = "HR Team",
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        // Connect the Microsoft account (seeded through the store's own one-upsert):
        // host, port, username, and FromAddress derive from the account; the stored
        // password is discarded.
        await factory.SeedMicrosoftAccountAsync("anna@outlook.com");

        var connected = await client.GetFromJsonAsync<SettingsContract>("/api/settings/smtp");
        Assert.NotNull(connected);
        Assert.Equal("settings", connected.Source);
        Assert.Equal("microsoft-account", connected.SignInMethod);
        Assert.Equal("smtp.office365.com", connected.Host);
        Assert.Equal(587, connected.Port);
        Assert.Equal("anna@outlook.com", connected.Username);
        Assert.Equal("anna@outlook.com", connected.FromAddress);
        Assert.Equal("HR Team", connected.FromName); // untouched by the connect
        Assert.False(connected.HasPassword); // the password is discarded

        // A Microsoft Account save is From-fields-only (decision 22).
        using var fromSave = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "microsoft-account",
            fromAddress = "anna@outlook.com",
            fromName = "Anna HR",
        });
        Assert.Equal(HttpStatusCode.OK, fromSave.StatusCode);
        var afterFromSave = await client.GetFromJsonAsync<SettingsContract>("/api/settings/smtp");
        Assert.NotNull(afterFromSave);
        Assert.Equal("microsoft-account", afterFromSave.SignInMethod);
        Assert.Equal("Anna HR", afterFromSave.FromName);
        Assert.Equal("smtp.office365.com", afterFromSave.Host);

        // Switching back to App Password discards the grant: a later Microsoft save
        // finds nothing connected and is refused (decision 11 + 30).
        using var switchBack = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "app-password",
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = "unit-secret",
            fromAddress = "hr@firma.example",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, switchBack.StatusCode);

        using var refused = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "microsoft-account",
            fromAddress = "anna@outlook.com",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("EmailSettings.MicrosoftSignInRequired", problem.Title);
        Assert.Equal("Connect a Microsoft account on this page before saving.", problem.Detail);
    }

    [Fact]
    public async Task Microsoft_save_requires_a_connected_grant() // domain: Sign-in Method — connect is the test; no grant, no save (decision 30)
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "microsoft-account",
            fromAddress = "anna@outlook.com",
            fromName = (string?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(problem);
        Assert.Equal("EmailSettings.MicrosoftSignInRequired", problem.Title);
    }

    [Fact]
    public async Task An_unreadable_grant_falls_back_to_the_configuration_file() // domain: SMTP Account — completeness is per method; an unreadable grant never fails every read (decision 29)
    {
        using var client = factory.CreateClient();

        // The first request runs the database reset; seeding must follow it.
        await client.GetAsync("/api/settings/smtp");
        await factory.SeedMicrosoftAccountAsync("anna@outlook.com");
        await factory.ExecuteSqlAsync("UPDATE smtp_settings SET protected_grant = 'not-ciphertext'");

        var settings = await client.GetFromJsonAsync<SettingsContract>("/api/settings/smtp");
        Assert.NotNull(settings);
        Assert.Equal("configuration-file", settings.Source);
        Assert.Equal("app-password", settings.SignInMethod);
        Assert.Equal("smtp.test.invalid", settings.Host);
    }

    [Fact]
    public async Task The_sender_checks_readiness_through_the_grant() // domain: Sign-in Method — the pre-flight refreshes silently; a dead grant reads SignInExpired (decisions 12, 20)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/api/settings/smtp"); // run the reset before seeding
        await factory.SeedMicrosoftAccountAsync("anna@outlook.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>();
        var sender = new MailKitEmailSender(store, factory.MicrosoftSignIn);

        // The Microsoft path performs one silent token acquisition (decision 20).
        Assert.Equal(SmtpReadiness.Configured, await sender.CheckReadinessAsync(CancellationToken.None));
        Assert.Equal(1, factory.MicrosoftSignIn.AcquireTokenCallCount);

        // A dead grant — revocation or network — reads SignInExpired, never a wall of
        // per-dispatch failures (decision 7).
        factory.MicrosoftSignIn.TokenException = new InvalidOperationException("network down");
        Assert.Equal(SmtpReadiness.SignInExpired, await sender.CheckReadinessAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_password_path_never_touches_the_token_seam() // domain: Sign-in Method — password readiness is completeness only, no network (decision 20)
    {
        using var client = factory.CreateClient();

        using var saveResponse = await client.PutAsJsonAsync("/api/settings/smtp", new
        {
            signInMethod = "app-password",
            host = "smtp.gmail.com",
            port = 587,
            username = "hr@firma.example",
            password = "unit-secret",
            fromAddress = "hr@firma.example",
            fromName = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>();
        var sender = new MailKitEmailSender(store, factory.MicrosoftSignIn);

        Assert.Equal(SmtpReadiness.Configured, await sender.CheckReadinessAsync(CancellationToken.None));
        Assert.Equal(0, factory.MicrosoftSignIn.AcquireTokenCallCount);
    }

    private sealed record ConnectChallengeContract(
        string UserCode,
        string VerificationUrl,
        DateTimeOffset ExpiresAt);

    private sealed record ConnectStatusContract(
        string State,
        string? AccountEmail,
        string? Error);

    private sealed record SettingsContract(
        string? Host,
        int? Port,
        string? Username,
        string? FromAddress,
        string? FromName,
        bool HasPassword,
        string? SignInMethod,
        bool MicrosoftSignInAvailable,
        string Source);

    private sealed record ProblemResponse(string Title, string Detail, int Status);
}
