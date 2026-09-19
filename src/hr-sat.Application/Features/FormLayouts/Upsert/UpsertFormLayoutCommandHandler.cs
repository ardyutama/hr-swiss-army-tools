using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

internal sealed class UpsertFormLayoutCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpsertFormLayoutCommand, FormLayoutResponse>
{
    public async Task<Result<FormLayoutResponse>> Handle(
        UpsertFormLayoutCommand command,
        CancellationToken cancellationToken)
    {
        var upsertResult = await VacancyWrite.ExecuteLockedAsync(
            command.VacancyId,
            dbContext,
            async vacancy =>
            {
                await dbContext.IntakeRounds
                    .Where(round => round.VacancyId == command.VacancyId)
                    .LoadAsync(cancellationToken);

                var result = vacancy.UpsertFormLayout(new FormLayoutDefinition(command.Columns));
                if (result.IsFailure)
                {
                    return Result<FormLayoutResponse>.Failure(result.Error);
                }

                var candidatesUpdated = 0;
                var typedOverridesKept = 0;
                if (vacancy.ActiveRound is not null)
                {
                    var candidates = await dbContext.Candidates
                        .Where(candidate =>
                            candidate.IntakeRoundId == vacancy.ActiveRound.Id &&
                            candidate.IntakeSource == CandidateIntakeSource.Form)
                        .Include(candidate => candidate.FormResponses)
                        .ToListAsync(cancellationToken);
                    foreach (var candidate in candidates)
                    {
                        var prefillResult = candidate.PrefillDetailsFromLayout(result.Value);
                        if (prefillResult.Updated)
                        {
                            candidatesUpdated++;
                        }

                        typedOverridesKept += prefillResult.TypedOverridesKept;
                    }
                }

                return FormLayoutResponse.From(
                    result.Value,
                    candidatesUpdated,
                    typedOverridesKept);
            },
            cancellationToken);
        if (upsertResult.IsFailure)
        {
            return Result<FormLayoutResponse>.Failure(upsertResult.Error);
        }

        return upsertResult.Value;
    }
}