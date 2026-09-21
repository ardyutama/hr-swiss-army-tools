namespace hr_sat.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }

    // The project-owned public client for Microsoft Account sign-in (issue 03,
    // decision 2) — a public-client ID is not a secret. Absent = the sign-in method
    // is unavailable; the client disables the choice (decision 24).
    public string? MicrosoftClientId { get; set; }

    // An absent or effectively-empty section means the installation cannot send.
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
