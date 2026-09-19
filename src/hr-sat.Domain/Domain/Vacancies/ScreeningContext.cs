using hr_sat.Domain.Vacancies.FormLayouts;

namespace hr_sat.Domain.Vacancies;

public sealed class ScreeningContext
{
    private ScreeningContext(
        ScreeningContextKind kind,
        ScreeningRuleSet? ruleSet,
        FormLayout? layout)
    {
        Kind = kind;
        RuleSet = ruleSet;
        Layout = layout;
    }

    internal ScreeningContextKind Kind { get; }
    internal ScreeningRuleSet? RuleSet { get; }
    internal FormLayout? Layout { get; }

    public static ScreeningContext Frozen { get; } = new(ScreeningContextKind.Frozen, null, null);
    public static ScreeningContext None { get; } = new(ScreeningContextKind.None, null, null);

    public static ScreeningContext Live(ScreeningRuleSet ruleSet, FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(layout);
        return new ScreeningContext(ScreeningContextKind.Live, ruleSet, layout);
    }

    public static ScreeningContext Of(ScreeningRuleSet? ruleSet, FormLayout? layout) =>
        ruleSet is null || layout is null ? None : Live(ruleSet, layout);
}
