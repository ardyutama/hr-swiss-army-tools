using hr_sat.Application.Features.EmailSettings.TestSmtpConnection;
using hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.EmailSettings;

// The server mirrors the client's input rules and owns them (issue 02, decision 6):
// bare hostname or IP, port 1–65535, non-empty username, strict From address, capped
// From name; the password stays write-only and optional on save. The rules are
// method-conditional (issue 03, decision 30): Microsoft Account validates only the
// From fields — the credential columns are derived from the connected account.
public sealed class EmailSettingsValidatorsTests
{
    private static readonly UpsertSmtpSettingsCommand ValidUpsert = new(
        "app-password",
        "smtp.gmail.com",
        587,
        "hr@firma.example",
        "app-password",
        "hr@firma.example",
        "HR Team");

    [Fact]
    public void Validate_Should_AcceptACompleteUpsert() // domain: SMTP Account
    {
        new UpsertSmtpSettingsCommandValidator().Validate(ValidUpsert).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_RejectSchemesAndPaths_InTheHost() // domain: SMTP Account
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        foreach (var host in new[] { "https://smtp.gmail.com", "smtp.gmail.com/mail", "", "   " })
        {
            var result = validator.Validate(ValidUpsert with { Host = host });
            result.IsValid.ShouldBeFalse($"host '{host}' must be rejected");
            result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(UpsertSmtpSettingsCommand.Host));
        }

        validator.Validate(ValidUpsert with { Host = "192.168.1.10" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_KeepPortInRange() // domain: SMTP Account
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        validator.Validate(ValidUpsert with { Port = 0 }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { Port = 65536 }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { Port = null }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { Port = 465 }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_RequireAUsername_ButNotItsShape() // domain: SMTP Account — strict email shape is a client-side preset rule only
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        validator.Validate(ValidUpsert with { Username = " " }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { Username = "EXCHANGE\\hr" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_KeepThePasswordWriteOnlySemantics() // domain: SMTP Account — null keeps the stored one, whitespace is not a password
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        validator.Validate(ValidUpsert with { Password = null }).IsValid.ShouldBeTrue();
        validator.Validate(ValidUpsert with { Password = "   " }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_Should_RequireAStrictFromAddress() // domain: SMTP Account
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        validator.Validate(ValidUpsert with { FromAddress = "HR <hr@firma.example>" }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { FromAddress = "not-an-email" }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { FromAddress = " " }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { FromAddress = "hr@firma.example" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_CapTheFromName() // domain: SMTP Account
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        validator.Validate(ValidUpsert with { FromName = new string('x', 201) }).IsValid.ShouldBeFalse();
        validator.Validate(ValidUpsert with { FromName = new string('x', 200) }).IsValid.ShouldBeTrue();
        validator.Validate(ValidUpsert with { FromName = null }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_ValidateTheProbeValues_ForTestConnection() // domain: SMTP Account
    {
        var validator = new TestSmtpConnectionCommandValidator();

        validator.Validate(new TestSmtpConnectionCommand("smtp.gmail.com", 587, "hr@firma.example", null))
            .IsValid.ShouldBeTrue();
        var result = validator.Validate(new TestSmtpConnectionCommand("smtp.gmail.com/x", 0, " ", null));
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(TestSmtpConnectionCommand.Host));
        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(TestSmtpConnectionCommand.Port));
        result.Errors.Select(error => error.PropertyName).ShouldContain(nameof(TestSmtpConnectionCommand.Username));
    }

    [Fact]
    public void Validate_Should_RequireADefinedSignInMethod() // domain: Sign-in Method — defined wire values only (issue 03, decision 16)
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        foreach (var method in new[] { null, "", "password", "microsoft", "basic", "oauth2" })
        {
            var result = validator.Validate(ValidUpsert with { SignInMethod = method });
            result.IsValid.ShouldBeFalse($"method '{method}' must be rejected");
            result.Errors.Select(error => error.PropertyName)
                .ShouldContain(nameof(UpsertSmtpSettingsCommand.SignInMethod));
        }

        validator.Validate(ValidUpsert with { SignInMethod = "App-Password" }).IsValid.ShouldBeTrue();
        validator.Validate(ValidUpsert with { SignInMethod = "microsoft-account", Host = null, Port = null, Username = null })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_SkipCredentialRules_WhenMicrosoftAccount() // domain: Sign-in Method — microsoft validates only the From fields (issue 03, decision 22)
    {
        var validator = new UpsertSmtpSettingsCommandValidator();

        var result = validator.Validate(ValidUpsert with
        {
            SignInMethod = "microsoft-account",
            Host = null,
            Port = null,
            Username = null,
            Password = null,
            FromAddress = "anna@outlook.com",
            FromName = null,
        });
        result.IsValid.ShouldBeTrue();

        // The From rules still bind under Microsoft Account.
        var invalid = validator.Validate(ValidUpsert with
        {
            SignInMethod = "microsoft-account",
            FromAddress = "not-an-email",
        });
        invalid.IsValid.ShouldBeFalse();
        invalid.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertSmtpSettingsCommand.FromAddress));
        invalid.Errors.Select(error => error.PropertyName)
            .ShouldNotContain(nameof(UpsertSmtpSettingsCommand.Host));
    }
}
