using hr_sat.Application.Abstractions.Email;
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
                SmtpSettingsSource.Settings)));
        var handler = new GetSmtpSettingsQueryHandler(store);

        var result = await handler.Handle(new GetSmtpSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var response = result.Value;
        response.Host.ShouldBe("smtp.gmail.com");
        response.Port.ShouldBe(587);
        response.Username.ShouldBe("hr@firma.example");
        response.FromAddress.ShouldBe("hr@firma.example");
        response.FromName.ShouldBe("HR Team");
        response.HasPassword.ShouldBeTrue();
        response.Source.ShouldBe("settings");
    }

    [Fact]
    public async Task Handle_Should_MapNullFields_WhenNothingIsConfigured() // domain: SMTP Account — source 'none' means the installation cannot send
    {
        var store = Substitute.For<ISmtpSettingsStore>();
        store.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SmtpSettingsSnapshot(
                null, null, null, null, null, false, SmtpSettingsSource.None)));
        var handler = new GetSmtpSettingsQueryHandler(store);

        var result = await handler.Handle(new GetSmtpSettingsQuery(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Source.ShouldBe("none");
        result.Value.Host.ShouldBeNull();
        result.Value.Port.ShouldBeNull();
        result.Value.HasPassword.ShouldBeFalse();
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
                SmtpSettingsSource.Settings)));
        var handler = new UpsertSmtpSettingsCommandHandler(store);

        var result = await handler.Handle(
            new UpsertSmtpSettingsCommand(
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

        // A whitespace-only password means "keep the stored one" — never persisted.
        written.ShouldNotBeNull();
        written.Password.ShouldBeNull();
        written.Host.ShouldBe("smtp.office365.com");
        written.Port.ShouldBe(587);
        written.FromName.ShouldBeNull();
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
}
