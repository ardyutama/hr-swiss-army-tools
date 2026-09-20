using hr_sat.Application.Abstractions.Messaging;

namespace hr_sat.Application.Features.Dispatches.RetryFailed;

public sealed record RetryFailedDispatchesCommand(long VacancyId, long RoundId, long RunId)
    : ICommand<DispatchRunReportResponse>;
