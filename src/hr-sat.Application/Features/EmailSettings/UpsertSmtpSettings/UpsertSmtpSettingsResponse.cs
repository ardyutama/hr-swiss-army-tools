namespace hr_sat.Application.Features.EmailSettings.UpsertSmtpSettings;

// The effective settings right after the upsert; the client patches its status line
// from this body instead of re-querying. Same shape as the GET response, owned per
// slice; the password is never carried.
public sealed record UpsertSmtpSettingsResponse(
    string? Host,
    int? Port,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool HasPassword,
    string Source);
