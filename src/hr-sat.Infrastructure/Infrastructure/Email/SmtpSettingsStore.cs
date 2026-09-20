using System.Security.Cryptography;
using hr_sat.Application.Abstractions.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace hr_sat.Infrastructure.Email;

// The SMTP Account store (issue 02, decisions 1–2): a complete saved row wins; an
// absent or incomplete row falls back to the Smtp configuration section (issue 01's
// bootstrap path). Protect/unprotect live here — handlers never see ciphertext.
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
    // Data Protection purpose string (decision 1). Honest limit, documented on the
    // issue: any process running as the installing user can unprotect — this is
    // at-rest protection, not access control.
    private const string ProtectorPurpose = "smtp-settings-v1";
    private const long SingletonId = 1;

    private Task<ResolvedSmtpSettings>? resolution;

    public async Task<SmtpSettingsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).Snapshot;

    public async Task<bool> HasStoredPasswordAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).StoredPassword is not null;

    public async Task SaveAsync(SmtpSettingsWrite write, CancellationToken cancellationToken)
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

        row.Host = write.Host.Trim();
        row.Port = write.Port;
        row.Username = write.Username.Trim();
        row.FromAddress = write.FromAddress.Trim();
        row.FromName = string.IsNullOrWhiteSpace(write.FromName) ? null : write.FromName.Trim();
        if (write.Password is not null)
        {
            row.ProtectedPassword = CreateProtector().Protect(write.Password.Trim());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        resolution = null;
    }

    public async Task RemoveAsync(CancellationToken cancellationToken)
    {
        await dbContext.Set<SmtpSettingsRow>().ExecuteDeleteAsync(cancellationToken);
        resolution = null;
    }

    // The effective connection for a send, password unprotected; null when the
    // installation cannot send.
    internal async Task<EffectiveSmtpConnection?> ResolveConnectionAsync(
        CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).Connection;

    // The saved row's own password, unprotected — the test endpoint's fallback when the
    // write-only field was left untouched. Never the file section's password.
    internal async Task<string?> ResolveStoredPasswordAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).StoredPassword;

    private Task<ResolvedSmtpSettings> ResolveAsync(CancellationToken cancellationToken) =>
        resolution ??= LoadAsync(cancellationToken);

    private async Task<ResolvedSmtpSettings> LoadAsync(CancellationToken cancellationToken)
    {
        var row = await dbContext.Set<SmtpSettingsRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        var storedPassword = ReadStoredPassword(row);

        // A complete saved row is the effective config; an absent or incomplete row
        // falls back to the configuration file (decision 2).
        if (row is not null &&
            storedPassword is not null &&
            !string.IsNullOrWhiteSpace(row.Host) &&
            !string.IsNullOrWhiteSpace(row.Username) &&
            !string.IsNullOrWhiteSpace(row.FromAddress))
        {
            return new ResolvedSmtpSettings(
                new SmtpSettingsSnapshot(
                    row.Host,
                    row.Port,
                    row.Username,
                    row.FromAddress,
                    row.FromName,
                    HasPassword: true,
                    SmtpSettingsSource.Settings),
                new EffectiveSmtpConnection(
                    row.Host,
                    row.Port,
                    row.Username,
                    storedPassword,
                    row.FromAddress,
                    row.FromName),
                storedPassword);
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
                    SmtpSettingsSource.ConfigurationFile),
                new EffectiveSmtpConnection(
                    file.Host!,
                    file.Port,
                    file.Username!,
                    file.Password!,
                    file.FromAddress!,
                    file.FromName),
                storedPassword);
        }

        return new ResolvedSmtpSettings(
            new SmtpSettingsSnapshot(null, null, null, null, null, false, SmtpSettingsSource.None),
            null,
            storedPassword);
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

    private IDataProtector CreateProtector() =>
        dataProtectionProvider.CreateProtector(ProtectorPurpose);
}
