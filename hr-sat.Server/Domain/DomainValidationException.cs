namespace hr_sat.Server.Domain;

public abstract class DomainValidationException(
    IReadOnlyDictionary<string, string[]> errors,
    string message) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}