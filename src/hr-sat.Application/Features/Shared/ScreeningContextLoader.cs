using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Shared;

internal static class ScreeningContextLoader
{
    public static async Task<ScreeningContext> LoadAsync(
        long vacancyId,
        bool roundClosed,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (roundClosed)
        {
            return ScreeningContext.Frozen;
        }

        var contexts = await LoadLiveAsync([vacancyId], dbContext, cancellationToken);
        return contexts.GetValueOrDefault(vacancyId, ScreeningContext.None);
    }

    public static async Task<IReadOnlyDictionary<long, ScreeningContext>> LoadLiveAsync(
        IReadOnlyCollection<long> vacancyIds,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var layouts = await dbContext.FormLayouts
            .AsNoTracking()
            .Where(layout => vacancyIds.Contains(layout.VacancyId))
            .ToDictionaryAsync(layout => layout.VacancyId, cancellationToken);
        var ruleSets = await dbContext.ScreeningRuleSets
            .AsNoTracking()
            .Where(ruleSet => vacancyIds.Contains(ruleSet.VacancyId))
            .ToDictionaryAsync(ruleSet => ruleSet.VacancyId, cancellationToken);

        return vacancyIds
            .Select(id => new
            {
                Id = id,
                Context = ScreeningContext.Of(
                    ruleSets.GetValueOrDefault(id),
                    layouts.GetValueOrDefault(id))
            })
            .Where(item => item.Context.Kind is not ScreeningContextKind.None)
            .ToDictionary(item => item.Id, item => item.Context);
    }
}
