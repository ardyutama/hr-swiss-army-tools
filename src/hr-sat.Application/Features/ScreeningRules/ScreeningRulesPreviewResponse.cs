namespace hr_sat.Application.Features.ScreeningRules;

public sealed record ScreeningRulesPreviewResponse(
    int Total,
    int ScreenedOut,
    IReadOnlyList<ScreeningRulePreviewResponse> PerRule);

public sealed record ScreeningRulePreviewResponse(int Index, int ScreenedOut);