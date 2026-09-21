using hr_sat.Application.Abstractions.Email;
using hr_sat.Application.Features.EmailSettings.BeginMicrosoftConnect;
using hr_sat.Application.Features.EmailSettings.GetMicrosoftConnectStatus;
using hr_sat.Application.Features.EmailSettings.GetSmtpSettings;
using hr_sat.Application.Features.EmailSettings.RemoveSmtpSettings;
using hr_sat.Application.Features.EmailSettings.TestSmtpConnection;
using hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;
using hr_sat.Domain;
using NSubstitute;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.EmailSettings;

public sealed class EmailSettingsHandlerTests
{
    [Fact]
    public async Task Handle_Should_MapTheEffectiveSnapshot_WhenGettingSettings() // domain: SMTP Account — the status line states what actually sends mail
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                "smtp.gmail.com",
                587,
                "hr@firma.example",
                "hr@firma.example",
                "HR Team",
                true,
                SignInMethod.AppPassword,
                SmtpSettingsSource.Settings)));
        var signIn = new FakeMicrosoftAccountSignIn();
        var handler = new GetSmtpSettingsQueryHandler(store, signIn);

        var result = await handler.Handle(new GetSmtpSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var response = result.Value;
        response.Host.ShouldBe("smtp.gmail.com");
        response.Port.ShouldBe(587);
        response.Username.ShouldBe("hr@firma.example");
        response.FromAddress.ShouldBe("hr@firma.example");
        response.FromName.ShouldBe("HR Team");
        response.HasPassword.ShouldBeTrue();
        response.SignInMethod.ShouldBe("app-password");
        response.MicrosoftSignInAvailable.ShouldBeTrue();
        response.Source.ShouldBe("settings");
    }

    [Fact]
    public async Task Handle_Should_MapNullFields_WhenNothingIsConfigured() // domain: SMTP Account — source 'none' means the installation cannot send
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                null, null, null, null, null, false, null, SmtpSettingsSource.None)));
        var signIn = new FakeMicrosoftAccountSignIn { IsAvailable = false };
        var handler = new GetSmtpSettingsQueryHandler(store, signIn);

        var result = await handler.Handle(new GetSmtpSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Source.ShouldBe("none");
        result.Value.Host.ShouldBeNull();
        result.Value.Port.ShouldBeNull();
        result.Value.HasPassword.ShouldBeFalse();
        result.Value.SignInMethod.ShouldBeNull();
        result.Value.MicrosoftSignInAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnTestFailed_WhenTheProbeFails() // domain: SMTP Account — the refusal names the stage, sanitized
    {
        var tester = Substitute.For<ISmtpConnectionTester>();
        tester.TestAsync(Arg.Any<SmtpTestRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SmtpTestResult.Failure(
                "Authentication failed — check the app password.")));
        var handler = new TestSmtpConnectionCommandHandler(
            Substitute.For<ISmtpSettingsStore>(),
            tester);

        var result = await handler.Handle(
            new TestSmtpConnectionCommand("smtp.gmail.com", 587, "hr@firma.example", "app-password"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EmailSettings.TestFailed");
        result.Error.Message.ShouldBe("Authentication failed — check the app password.");
    }

    [Fact]
    public async Task Handle_Should_RefuseTheTest_WhenNoPasswordWasTypedAndNoneIsStored() // domain: SMTP Account — the write-only field must be filled on first setup
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        store.HasStoredPasswordAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var tester = Substitute.For<ISmtpConnectionTester>();
        var handler = new TestSmtpConnectionCommandHandler(store, tester);

        var result = await handler.Handle(
            new TestSmtpConnectionCommand("smtp.gmail.com", 587, "hr@firma.example", null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Code.ShouldBe("EmailSettings.Invalid");
        error.Errors.Keys.ShouldContain("password");
        await tester.DidNotReceive().TestAsync(Arg.Any<SmtpTestRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_LetTheTesterFallBack_WhenThePasswordFieldIsUntouched() // domain: SMTP Account — omitted password keeps the stored one
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        store.HasStoredPasswordAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        SmtpTestRequest? probed = null;
        var tester = Substitute.For<ISmtpConnectionTester>();
        tester.TestAsync(Arg.Do<SmtpTestRequest>(request => probed = request), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(SmtpTestResult.Success));
        var handler = new TestSmtpConnectionCommandHandler(store, tester);

        var result = await handler.Handle(
            new TestSmtpConnectionCommand(" smtp.gmail.com ", 587, " hr@firma.example ", "   "),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Message.ShouldBe("Connected and authenticated.");
        probed.ShouldNotBeNull();
        probed.Password.ShouldBeNull();
        probed.Host.ShouldBe("smtp.gmail.com");
        probed.Username.ShouldBe("hr@firma.example");
    }

    [Fact]
    public async Task Handle_Should_SaveAndReturnTheFreshSnapshot_WhenUpserting() // domain: SMTP Account — the save response is the status line's success feedback
    {
        SmtpSettingsWrite? written = null;
        var store = Substitute.For<ISmtpSettingsStore>();
        store.SaveAsync(Arg.Do<SmtpSettingsWrite>(value => written = value), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                "smtp.office365.com",
                587,
                "hr@firma.example",
                "hr@firma.example",
                null,
                true,
                SignInMethod.AppPassword,
                SmtpSettingsSource.Settings)));
        var handler = new UpsertSmtpSettingsCommandHandler(store, new FakeMicrosoftAccountSignIn());

        var result = await handler.Handle(
            new UpsertSmtpSettingsCommand(
                "app-password",
                "smtp.office365.com",
                587,
                "hr@firma.example",
                "  ",
                "hr@firma.example",
                null),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Source.ShouldBe("settings");
        result.Value.Host.ShouldBe("smtp.office365.com");
        result.Value.SignInMethod.ShouldBe("app-password");
        result.Value.MicrosoftSignInAvailable.ShouldBeTrue();

        // A whitespace-only password means "keep the stored one" — never persisted.
        var appPassword = written.ShouldBeOfType<SmtpSettingsWrite.AppPassword>();
        appPassword.Password.ShouldBeNull();
        appPassword.Host.ShouldBe("smtp.office365.com");
        appPassword.Port.ShouldBe(587);
        appPassword.FromName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_SaveFromFieldsOnly_WhenUpsertingAMicrosoftAccount() // domain: Sign-in Method — the server derives the credential columns (issue 03, decision 22)
    {
        SmtpSettingsWrite? written = null;
        var store = Substitute.For<ISmtpSettingsStore>();
        store.SaveAsync(Arg.Do<SmtpSettingsWrite>(value => written = value), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        // A connected Microsoft Account resolves as a settings row of its own method —
        // the grant exists and decrypts (decision 29's completeness).
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                "smtp.office365.com",
                587,
                "anna@outlook.com",
                "anna@outlook.com",
                null,
                false,
                SignInMethod.MicrosoftAccount,
                SmtpSettingsSource.Settings)));
        var handler = new UpsertSmtpSettingsCommandHandler(store, new FakeMicrosoftAccountSignIn());

        var result = await handler.Handle(
            new UpsertSmtpSettingsCommand(
                "microsoft-account",
                // Credential fields the client shouldn't have sent are silently ignored.
                "smtp.evil.example",
                2525,
                "mallory",
                "stolen",
                "anna@outlook.com",
                "Anna HR"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.SignInMethod.ShouldBe("microsoft-account");
        result.Value.Username.ShouldBe("anna@outlook.com");
        var microsoft = written.ShouldBeOfType<SmtpSettingsWrite.MicrosoftAccount>();
        microsoft.FromAddress.ShouldBe("anna@outlook.com");
        microsoft.FromName.ShouldBe("Anna HR");
    }

    [Fact]
    public async Task Handle_Should_RefuseTheMicrosoftSave_WhenNoGrantIsConnected() // domain: Sign-in Method — connect a Microsoft account first (issue 03, decision 30)
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        // No connected grant: the effective source is the configuration file (or none),
        // never a Microsoft settings row.
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                "smtp.test.invalid",
                587,
                "hr@test.invalid",
                "hr@test.invalid",
                null,
                true,
                SignInMethod.AppPassword,
                SmtpSettingsSource.ConfigurationFile)));
        var handler = new UpsertSmtpSettingsCommandHandler(store, new FakeMicrosoftAccountSignIn());

        var result = await handler.Handle(
            new UpsertSmtpSettingsCommand(
                "microsoft-account",
                null,
                null,
                null,
                null,
                "anna@outlook.com",
                null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EmailSettings.MicrosoftSignInRequired");
        result.Error.Message.ShouldBe("Connect a Microsoft account on this page before saving.");
        await store.DidNotReceive().SaveAsync(Arg.Any<SmtpSettingsWrite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_RemoveSavedSettings_WhenAsked() // domain: SMTP Account — the escape hatch returns to the configuration file
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        var handler = new RemoveSmtpSettingsCommandHandler(store);

        var result = await handler.Handle(new RemoveSmtpSettingsCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await store.Received(1).RemoveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnTheChallenge_WhenBeginningAConnect() // domain: Sign-in Method — the connect panel shows the device code (issue 03, decision 32)
    {
        var signIn = new FakeMicrosoftAccountSignIn();
        var handler = new BeginMicrosoftConnectCommandHandler(signIn);

        var result = await handler.Handle(new BeginMicrosoftConnectCommand(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserCode.ShouldBe("ABCD-EFGH");
        result.Value.VerificationUrl.ShouldBe("https://microsoft.com/link");
        result.Value.ExpiresAt.ShouldBe(new DateTimeOffset(2026, 9, 21, 12, 15, 0, TimeSpan.Zero));
        signIn.BeginCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_RefuseTheBegin_WhenMicrosoftSignInIsUnavailable() // domain: Sign-in Method — no client ID, no Microsoft choice (issue 03, decision 24)
    {
        var signIn = new FakeMicrosoftAccountSignIn { IsAvailable = false };
        var handler = new BeginMicrosoftConnectCommandHandler(signIn);

        var result = await handler.Handle(new BeginMicrosoftConnectCommand(), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("EmailSettings.MicrosoftSignInUnavailable");
        signIn.BeginCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReportTheConnectStatus_WhenPolling() // domain: Sign-in Method — idle, pending, and the terminal states ride the wire lowercase (issue 03, decisions 21, 32)
    {
        var signIn = new FakeMicrosoftAccountSignIn();
        var handler = new GetMicrosoftConnectStatusQueryHandler(signIn);

        var idle = await handler.Handle(new GetMicrosoftConnectStatusQuery(), CancellationToken.None);
        idle.Value.State.ShouldBe("idle");
        idle.Value.AccountEmail.ShouldBeNull();

        signIn.SetPending();
        var pending = await handler.Handle(new GetMicrosoftConnectStatusQuery(), CancellationToken.None);
        pending.Value.State.ShouldBe("pending");

        signIn.SetSucceeded("anna@outlook.com");
        var succeeded = await handler.Handle(new GetMicrosoftConnectStatusQuery(), CancellationToken.None);
        succeeded.Value.State.ShouldBe("succeeded");
        succeeded.Value.AccountEmail.ShouldBe("anna@outlook.com");
        succeeded.Value.Error.ShouldBeNull();

        signIn.SetFailed("The sign-in was declined before it finished.");
        var failed = await handler.Handle(new GetMicrosoftConnectStatusQuery(), CancellationToken.None);
        failed.Value.State.ShouldBe("failed");
        failed.Value.Error.ShouldBe("The sign-in was declined before it finished.");

        signIn.SetExpired();
        var expired = await handler.Handle(new GetMicrosoftConnectStatusQuery(), CancellationToken.None);
        expired.Value.State.ShouldBe("expired");
    }
}
