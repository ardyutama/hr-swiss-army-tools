namespace hr_sat.Domain;

public enum ErrorType
{
    Failure,
    Validation,
    Problem,
    NotFound,
    Conflict
}

public record Error(
    string Code,
    string Message,
    ErrorType Type,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static Error None => new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Failure, extensions);

    public static Error Validation(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Validation, extensions);

    public static Error Problem(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Problem, extensions);

    public static Error NotFound(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.NotFound, extensions);

    public static Error Conflict(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(code, message, ErrorType.Conflict, extensions);
}

public sealed record ValidationError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors)
    : Error(Code, Message, ErrorType.Validation);