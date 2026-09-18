using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateDetailsRules
{
    public static Result<(string FullName, string ContactEmail, string? ContactPhone)> Validate(
        string? fullName,
        string? contactEmail,
        string? contactPhone)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedFullName = TryNormalizeRequired(fullName, 300, out var fullNameValue);
        if (!normalizedFullName)
        {
            errors["fullName"] = ["Name must contain between 1 and 300 characters after trimming."];
        }

        var normalizedContactEmail = TryNormalizeRequired(contactEmail, 320, out var contactEmailValue);
        if (!normalizedContactEmail)
        {
            errors["contactEmail"] = ["Email must contain between 1 and 320 characters after trimming."];
        }

        if (!TryNormalizeOptional(contactPhone, 100, out var contactPhoneValue))
        {
            errors["contactPhone"] = ["Phone must contain 100 characters or fewer after trimming."];
        }

        return errors.Count > 0
            ? Result<(string FullName, string ContactEmail, string? ContactPhone)>.Failure(CandidateErrors.Invalid(errors))
            : (fullNameValue!, contactEmailValue!, contactPhoneValue);
    }

    public static bool TryNormalizeFullName(string? value, out string? normalized) =>
        TryNormalizeRequired(value, 300, out normalized);

    public static bool TryNormalizeContactEmail(string? value, out string? normalized) =>
        TryNormalizeRequired(value, 320, out normalized);

    public static bool TryNormalizeContactPhone(string? value, out string? normalized) =>
        TryNormalizeOptional(value, 100, out normalized);

    private static bool TryNormalizeRequired(
        string? value,
        int maxLength,
        out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is not null && normalized.Length <= maxLength;
    }

    private static bool TryNormalizeOptional(
        string? value,
        int maxLength,
        out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= maxLength;
    }
}