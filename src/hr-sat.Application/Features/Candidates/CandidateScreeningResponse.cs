using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.Candidates;

public sealed record CandidateScreeningResponse(
    bool ScreenedOut,
    IReadOnlyList<FiredScreeningRuleResponse> FiredRules)
{
    public static CandidateScreeningResponse From(
        bool screenedOut,
        IReadOnlyList<ScreeningRuleMatch>? firedRules) => new(
        screenedOut,
        firedRules?
            .Select(rule => new FiredScreeningRuleResponse(rule.Index, rule.Display))
            .ToArray() ?? []);
}

public sealed record FiredScreeningRuleResponse(
    int Index,
    string Display);