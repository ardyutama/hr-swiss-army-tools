using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.GetMicrosoftConnectStatus;

// The status poll of the in-flight Microsoft Account connect attempt (issue 03,
// decision 32): the client polls ~2s to a terminal state.
public sealed record GetMicrosoftConnectStatusQuery : IQuery<MicrosoftConnectStatusResponse>;
