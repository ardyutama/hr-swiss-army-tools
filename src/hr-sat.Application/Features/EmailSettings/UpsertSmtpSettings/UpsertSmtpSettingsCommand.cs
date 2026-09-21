using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

// The saved SMTP Account values, discriminated by Sign-in Method (issue 03, decisions
// 11, 22): 'app-password' carries the full credential set (a null Password keeps the
// stored one — the write-only field submits only when typed into, decision 6);
// 'microsoft-account' carries only the From fields — the server derives the credential
// columns from the connected account and silently ignores any it shouldn't have been
// sent.
public sealed record UpsertSmtpSettingsCommand(
    string? SignInMethod,
    string? Host,
    int? Port,
    string? Username,
    string? Password,
    string? FromAddress,
    string? FromName) : ICommand<UpsertSmtpSettingsResponse>;
