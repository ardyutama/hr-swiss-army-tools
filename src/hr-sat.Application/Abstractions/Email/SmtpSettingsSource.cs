namespace hr_sat.Application.Abstractions.Email;

// Where the effective SMTP Account comes from (issue 02, decision 12): a complete
// saved settings row, the configuration-file fallback, or nowhere (the installation
// cannot send).
public enum SmtpSettingsSource
{
    Settings,
    ConfigurationFile,
    None,
}
