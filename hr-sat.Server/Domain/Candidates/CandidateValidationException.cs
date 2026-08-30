namespace hr_sat.Server.Domain.Candidates;

public sealed class CandidateValidationException(
    IReadOnlyDictionary<string, string[]> errors) : Exception("The candidate is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}