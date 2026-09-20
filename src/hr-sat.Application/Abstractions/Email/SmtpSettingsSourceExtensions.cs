namespace hr_sat.Application.Abstractions.Email;

public static class SmtpSettingsSourceExtensions
{
    public static string ToApiValue(this SmtpSettingsSource source) => source switch
    {
        SmtpSettingsSource.Settings => "settings",
        SmtpSettingsSource.ConfigurationFile => "configuration-file",
        SmtpSettingsSource.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };
}
