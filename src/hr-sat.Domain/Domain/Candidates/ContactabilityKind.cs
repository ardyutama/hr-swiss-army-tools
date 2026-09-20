namespace hr_sat.Domain.Candidates;

public enum ContactabilityKind
{
    Undecided,
    OutcomeRecorded,
    MissingEmail,
    Contactable
}

public static class ContactabilityKindExtensions
{
    public static string ToApiValue(this ContactabilityKind kind) => kind switch
    {
        ContactabilityKind.Undecided => "undecided",
        ContactabilityKind.OutcomeRecorded => "outcome-recorded",
        ContactabilityKind.MissingEmail => "missing-email",
        ContactabilityKind.Contactable => "contactable",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown contactability kind.")
    };
}
