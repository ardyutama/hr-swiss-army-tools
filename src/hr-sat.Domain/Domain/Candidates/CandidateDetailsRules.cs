using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateDetailsRules
{
    public static Result<(string FullName, string ContactEmail)> Validate(
        string? fullName,
        string? contactEmail)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 300)
        {
            errors["fullName"] = ["Name must contain between 1 and 300 characters after trimming."];
        }

        if (string.IsNullOrWhiteSpace(contactEmail) || contactEmail.Trim().Length > 320)
        {
            errors["contactEmail"] = ["Email must contain between 1 and 320 characters after trimming."];
        }

        return errors.Count > 0
            ? Result<(string FullName, string ContactEmail)>.Failure(CandidateErrors.Invalid(errors))
            : Result<(string FullName, string ContactEmail)>.Success((fullName!.Trim(), contactEmail!.Trim()));
    }
}