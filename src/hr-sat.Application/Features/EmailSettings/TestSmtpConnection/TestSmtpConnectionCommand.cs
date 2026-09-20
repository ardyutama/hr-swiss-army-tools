using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.TestSmtpConnection;

// The unsaved form values (issue 02, decision 3): the probe tests what the user typed
// and persists nothing. A null Password falls back to the stored row's password — the
// upsert's keep-semantics applied to the write-only field (decision 12).
public sealed record TestSmtpConnectionCommand(
    string? Host,
    int? Port,
    string? Username,
    string? Password) : ICommand<TestSmtpConnectionResponse>;
