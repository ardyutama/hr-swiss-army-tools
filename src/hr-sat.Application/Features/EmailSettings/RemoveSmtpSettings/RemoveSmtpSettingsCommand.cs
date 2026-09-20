using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.RemoveSmtpSettings;

// The escape hatch (issue 02, decision 11): removes the saved row so the configuration
// file (if any) resumes. Idempotent — removing when nothing is saved still succeeds.
public sealed record RemoveSmtpSettingsCommand : ICommand;
