using hr_sat.Server.Domain;

namespace hr_sat.Server.Domain.Candidates;

public sealed class CandidateValidationException(
    IReadOnlyDictionary<string, string[]> errors)
    : DomainValidationException(errors, "The candidate is invalid.");