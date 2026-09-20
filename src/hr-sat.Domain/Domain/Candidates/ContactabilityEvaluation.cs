using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Candidates;

public sealed record ContactabilityEvaluation(ContactabilityKind Kind, EmailTemplateKind? TemplateKind)
{
    public static readonly ContactabilityEvaluation Undecided =
        new(ContactabilityKind.Undecided, null);

    public static readonly ContactabilityEvaluation OutcomeRecorded =
        new(ContactabilityKind.OutcomeRecorded, null);

    public static readonly ContactabilityEvaluation MissingEmail =
        new(ContactabilityKind.MissingEmail, null);

    public static ContactabilityEvaluation Contactable(EmailTemplateKind decidedAs) =>
        new(ContactabilityKind.Contactable, decidedAs);
}
