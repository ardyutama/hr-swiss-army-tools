using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Dispatches.SendToAll;

public sealed record SendToAllCommand(long VacancyId, long RoundId)
    : ICommand<DispatchRunReportResponse>;
