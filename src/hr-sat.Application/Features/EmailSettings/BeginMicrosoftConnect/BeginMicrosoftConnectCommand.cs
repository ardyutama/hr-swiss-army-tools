using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.EmailSettings.BeginMicrosoftConnect;

// Starts a Microsoft Account connect attempt (issue 03, decision 32): the server
// begins the device-code flow and answers with the challenge to show the user.
public sealed record BeginMicrosoftConnectCommand : ICommand<MicrosoftConnectChallengeResponse>;
