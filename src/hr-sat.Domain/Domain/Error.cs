namespace hr_sat.Domain;

public enum ErrorType
{
    Failure,
    Validation,
    Problem,
    NotFound,
    Conflict
}

public record Error(string Code, string Message, ErrorType Type)
{
    public static Error None => new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string message) =>
        new(code, message, ErrorType.Failure);

    public static Error Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static Error Problem(string code, string message) =>
        new(code, message, ErrorType.Problem);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);
}

public sealed record ValidationError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]> Errors)
    : Error(Code, Message, ErrorType.Validation);