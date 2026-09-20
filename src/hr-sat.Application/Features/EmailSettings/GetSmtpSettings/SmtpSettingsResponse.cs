namespace hr_sat.Application.Features.EmailSettings.GetSmtpSettings;

// The settings page's read model (issue 02, decision 12): always 200, never carries
// the password — HasPassword flags a stored one so the client renders "Saved — type to
// replace". Source is 'settings', 'configuration-file', or 'none' (all fields null).
public sealed record SmtpSettingsResponse(
    string? Host,
    int? Port,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool HasPassword,
    string Source);
