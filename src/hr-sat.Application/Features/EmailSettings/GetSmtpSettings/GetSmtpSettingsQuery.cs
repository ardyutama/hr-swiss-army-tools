using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.GetSmtpSettings;

public sealed record GetSmtpSettingsQuery : IQuery<SmtpSettingsResponse>;
