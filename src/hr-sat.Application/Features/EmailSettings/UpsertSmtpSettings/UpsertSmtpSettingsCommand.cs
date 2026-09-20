using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

// The saved SMTP Account values (issue 02, decision 12). A null Password keeps the
// stored one — the write-only field submits only when typed into (decision 6).
public sealed record UpsertSmtpSettingsCommand(
    string? Host,
    int? Port,
    string? Username,
    string? Password,
    string? FromAddress,
    string? FromName) : ICommand<UpsertSmtpSettingsResponse>;
