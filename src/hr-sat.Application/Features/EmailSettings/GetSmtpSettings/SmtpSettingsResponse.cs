namespace hr_sat.Application.Features.EmailSettings.GetSmtpSettings;

// The settings page's read model (issue 02, decision 12): always 200, never carries
// the password — HasPassword flags a stored one so the client renders "Saved — type to
// replace". Source is 'settings', 'configuration-file', or 'none' (all fields null).
// SignInMethod is 'app-password' | 'microsoft-account' | null (issue 03, decisions
// 11, 16 — the configuration file is always App Password); MicrosoftSignInAvailable
// tells the client whether the Microsoft choice is offered at all (decision 24).
public sealed record SmtpSettingsResponse(
    string? Host,
    int? Port,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool HasPassword,
    string? SignInMethod,
    bool MicrosoftSignInAvailable,
    string Source);
