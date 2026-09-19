using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.ScreeningRules.Upsert;

internal sealed class UpsertScreeningRulesCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpsertScreeningRulesCommand, ScreeningRuleSetResponse>
{
    public async Task<Result<ScreeningRuleSetResponse>> Handle(
        UpsertScreeningRulesCommand command,
        CancellationToken cancellationToken)
    {
        var upsertResult = await VacancyWrite.ExecuteLockedAsync<ScreeningRuleSetResponse>(
            command.VacancyId,
            dbContext,
            vacancy =>
            {
                var result = vacancy.UpsertScreeningRules(
                    new ScreeningRuleDefinition(command.Rules));
                if (result.IsFailure)
                {
                    return Task.FromResult(
                        Result<ScreeningRuleSetResponse>.Failure(result.Error));
                }

                if (result.Value.Id == 0)
                {
                    dbContext.ScreeningRuleSets.Add(result.Value);
                }

                return Task.FromResult<Result<ScreeningRuleSetResponse>>(
                    ScreeningRuleSetResponse.From(result.Value));
            },
            cancellationToken);
        return upsertResult.IsFailure
            ? Result<ScreeningRuleSetResponse>.Failure(upsertResult.Error)
            : upsertResult.Value;
    }
}