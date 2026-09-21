using System.Security.Cryptography;
using hr_sat.Application.Abstractions.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace hr_sat.Infrastructure.Email;

// The SMTP Account store (issue 02, decisions 1–2): a complete saved row wins; an
// absent or incomplete row falls back to the Smtp configuration section (issue 01's
// bootstrap path). Completeness is per Sign-in Method (issue 03, decision 29): an
// App Password row needs a decryptable password, a Microsoft Account row needs a
// decryptable grant — an unreadable credential falls back to the file, never fails
// every read. Protect/unprotect live here — handlers never see ciphertext, and the
// store stays MSAL-ignorant (the grant is an opaque blob in/out, decision 9).
//
// Scoped, with the resolution pinned per scope (decision 13): a dispatch run sees one
// account from refusal check to last send, while the settings page's own request scope
// keeps the status line live. Save/Remove drop the pin so a later read in the same
// scope sees the new state.
public sealed class SmtpSettingsStore(
    AppDbContext dbContext,
    IOptions<SmtpOptions> fileOptions,
    IDataProtectionProvider dataProtectionProvider) : ISmtpSettingsStore
{
    // Data Protection purpose strings (issue 02 decision 1; issue 03 decision 9). The
    // grant gets its own purpose so the two credentials' protectors evolve
    // independently. Honest limit, documented on the issues: any process running as
    // the installing user can unprotect — this is at-rest protection, not access
    // control.
    private const string ProtectorPurpose = "smtp-settings-v1";
    private const string GrantProtectorPurpose = "smtp-microsoft-sign-in-v1";
    private const long SingletonId = 1;

    // The Outlook preset constants the connect flow derives (issue 03, decision 10) —
    // mirrored from the client's outlook preset; a Microsoft Account always sends
    // through them.
    private const string MicrosoftHost = "smtp.office365.com";
    private const int MicrosoftPort = 587;

    private Task<ResolvedSmtpSettings>? resolution;

    public async Task<SmtpSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).Snapshot;

    public async Task<bool> HasStoredPasswordAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).StoredPassword is not null;

    public async Task SaveAsync(SmtpSettingsWrite write, CancellationToken cancellationToken)
    {
        switch (write)
        {
            // App Password: the full upsert; switching methods discards the other
            // credential, so the grant is cleared (issue 03, decision 11).
            case SmtpSettingsWrite.AppPassword appPassword:
            {
                var row = await dbContext.Set<SmtpSettingsRow>()
                    .SingleOrDefaultAsync(cancellationToken);
                if (row is null)
                {
                    row = new SmtpSettingsRow
                    {
                        Id = SingletonId,
                        Host = string.Empty,
                        Username = string.Empty,
                        FromAddress = string.Empty,
                    };
                    dbContext.Set<SmtpSettingsRow>().Add(row);
                }

                row.SignInMethod = SignInMethod.AppPassword;
                row.Host = appPassword.Host.Trim();
                row.Port = appPassword.Port;
                row.Username = appPassword.Username.Trim();
                row.FromAddress = appPassword.FromAddress.Trim();
                row.FromName = string.IsNullOrWhiteSpace(appPassword.FromName)
                    ? null
                    : appPassword.FromName.Trim();
                row.ProtectedGrant = null;
                if (appPassword.Password is not null)
                {
                    row.ProtectedPassword = CreateProtector().Protect(appPassword.Password.Trim());
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                break;
            }

            // Microsoft Account: From-fields-only update — credential columns stay as
            // the connect flow derived them, the grant stays untouched, and the stored
            // password is discarded (issue 03, decisions 22, 29). Conditioned on the
            // row still being Microsoft-signed-in so a concurrent disconnect is a
            // no-op (the decision-23 conditioning applied to the settings save).
            case SmtpSettingsWrite.MicrosoftAccount microsoftAccount:
                await dbContext.Set<SmtpSettingsRow>()
                    .Where(row => row.SignInMethod == SignInMethod.MicrosoftAccount)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(row => row.FromAddress, microsoftAccount.FromAddress.Trim())
                            .SetProperty(
                                row => row.FromName,
                                string.IsNullOrWhiteSpace(microsoftAccount.FromName)
                                    ? null
                                    : microsoftAccount.FromName.Trim())
                            .SetProperty(row => row.ProtectedPassword, (string?)null),
                        cancellationToken);
                break;
        }

        resolution = null;
    }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await dbContext.Set<SmtpSettingsRow>().ExecuteDeleteAsync(cancellationToken);
        resolution = null;
    }

    // Concrete-only, off the ISmtpSettingsStore interface (issue 03, decision 29): the
    // connect completion path's single upsert — the Microsoft columns and the grant
    // land together so a half-connected row never exists (decision 23). The sender
    // identity is derived, not typed (decision 5): username and FromAddress are the
    // connected account's email; FromName is untouched. The other method's credential
    // is discarded (decision 11).
    public async Task ConnectMicrosoftAccountAsync(
        string accountEmail,
        string grantBlob,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Set<SmtpSettingsRow>()
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            row = new SmtpSettingsRow
            {
                Id = SingletonId,
                Host = string.Empty,
                Username = string.Empty,
                FromAddress = string.Empty,
            };
            dbContext.Set<SmtpSettingsRow>().Add(row);
        }

        row.SignInMethod = SignInMethod.MicrosoftAccount;
        row.Host = MicrosoftHost;
        row.Port = MicrosoftPort;
        row.Username = accountEmail.Trim();
        row.FromAddress = accountEmail.Trim();
        row.ProtectedGrant = CreateGrantProtector().Protect(grantBlob);
        row.ProtectedPassword = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        resolution = null;
    }

    // The effective connection for a send, credential materialized; null when the
    // installation cannot send.
    internal async Task<EffectiveSmtpConnection?> ResolveConnectionAsync(
        CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).Connection;

    // The saved row's own password, unprotected — the test endpoint's fallback when the
    // write-only field was left untouched. Never the file section's password.
    internal async Task<string?> ResolveStoredPasswordAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).StoredPassword;

    // Infrastructure-internal, off the interface (decision 23): the MSAL token-cache
    // callback's read of the stored grant blob (raw serialization, unprotected); null
    // when no decryptable grant exists.
    internal async Task<string?> ReadMicrosoftGrantAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).StoredGrant;

    // The silent-refresh callback's write (decision 23): grant-only, conditioned on
    // the row still being Microsoft-signed-in so a concurrent disconnect is a no-op.
    internal async Task UpdateMicrosoftGrantAsync(
        string grantBlob,
        CancellationToken cancellationToken)
    {
        var protectedGrant = CreateGrantProtector().Protect(grantBlob);
        await dbContext.Set<SmtpSettingsRow>()
            .Where(row => row.SignInMethod == SignInMethod.MicrosoftAccount)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(row => row.ProtectedGrant, protectedGrant),
                cancellationToken);
        resolution = null;
    }

    private Task<ResolvedSmtpSettings> ResolveAsync(CancellationToken cancellationToken) =>
        resolution ??= LoadAsync(cancellationToken);

    private async Task<ResolvedSmtpSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var row = await dbContext.Set<SmtpSettingsRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var storedPassword = ReadStoredPassword(row);
        var storedGrant = ReadStoredGrant(row);

        // A complete saved row is the effective config; an absent or incomplete row
        // falls back to the configuration file (decision 2). Completeness is per
        // Sign-in Method (decision 29).
        if (row is not null &&
            !string.IsNullOrWhiteSpace(row.Host) &&
            !string.IsNullOrWhiteSpace(row.Username) &&
            !string.IsNullOrWhiteSpace(row.FromAddress))
        {
            if (row.SignInMethod == SignInMethod.AppPassword && storedPassword is not null)
            {
                return new ResolvedSmtpSettings(
                    new SmtpSettingsSnapshot(
                        row.Host,
                        row.Port,
                        row.Username,
                        row.FromAddress,
                        row.FromName,
                        HasPassword: true,
                        SignInMethod.AppPassword,
                        SmtpSettingsSource.Settings),
                    new EffectiveSmtpConnection.AppPassword(
                        row.Host,
                        row.Port,
                        row.Username,
                        storedPassword,
                        row.FromAddress,
                        row.FromName),
                    storedPassword,
                    storedGrant);
            }

            if (row.SignInMethod == SignInMethod.MicrosoftAccount && storedGrant is not null)
            {
                return new ResolvedSmtpSettings(
                    new SmtpSettingsSnapshot(
                        row.Host,
                        row.Port,
                        row.Username,
                        row.FromAddress,
                        row.FromName,
                        HasPassword: false,
                        SignInMethod.MicrosoftAccount,
                        SmtpSettingsSource.Settings),
                    new EffectiveSmtpConnection.MicrosoftAccount(
                        row.Host,
                        row.Port,
                        row.Username,
                        row.FromAddress,
                        row.FromName),
                    storedPassword,
                    storedGrant);
            }
        }

        var file = fileOptions.Value;
        if (file.IsConfigured)
        {
            return new ResolvedSmtpSettings(
                new SmtpSettingsSnapshot(
                    file.Host,
                    file.Port,
                    file.Username,
                    file.FromAddress,
                    file.FromName,
                    !string.IsNullOrEmpty(file.Password),
                    SignInMethod.AppPassword,
                    SmtpSettingsSource.ConfigurationFile),
                new EffectiveSmtpConnection.AppPassword(
                    file.Host!,
                    file.Port,
                    file.Username!,
                    file.Password!,
                    file.FromAddress!,
                    file.FromName),
                storedPassword,
                storedGrant);
        }

        return new ResolvedSmtpSettings(
            new SmtpSettingsSnapshot(null, null, null, null, null, false, null, SmtpSettingsSource.None),
            null,
            storedPassword,
            storedGrant);
    }

    // A password whose Data Protection keys no longer exist can never be read again;
    // treat the row as incomplete so the file fallback resumes and the settings page
    // can simply be saved over, instead of failing every read.
    private string? ReadStoredPassword(SmtpSettingsRow? row)
    {
        if (row?.ProtectedPassword is null)
        {
            return null;
        }

        try
        {
            return CreateProtector().Unprotect(row.ProtectedPassword);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // The grant mirrors the password rule (decision 14): an unreadable grant makes the
    // row incomplete and falls back to the file; a decryptable-but-corrupt blob
    // surfaces later as SignInExpired (the store stays MSAL-ignorant, decision 29).
    private string? ReadStoredGrant(SmtpSettingsRow? row)
    {
        if (row?.ProtectedGrant is null)
        {
            return null;
        }

        try
        {
            return CreateGrantProtector().Unprotect(row.ProtectedGrant);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private IDataProtector CreateProtector() =>
        dataProtectionProvider.CreateProtector(ProtectorPurpose);

    private IDataProtector CreateGrantProtector() =>
        dataProtectionProvider.CreateProtector(GrantProtectorPurpose);
}
