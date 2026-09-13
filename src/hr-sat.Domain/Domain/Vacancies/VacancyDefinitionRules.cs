using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

internal static class VacancyDefinitionRules
{
    public static Result<List<string>> Validate(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        if (title is null || title.Trim().Length is < 1 or > 200)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["title"] = ["Title must contain between 1 and 200 characters after trimming."]
            }));
        }

        if (openedOn == default)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["openedOn"] = ["Opening Date is required."]
            }));
        }

        if (neededHires is < 1 or > 9999)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["neededHires"] = ["Needed Hires must be between 1 and 9999."]
            }));
        }

        var requirementList = requirements?.ToList() ?? [];
        if (requirementList.Count == 0)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] = ["At least one vacancy requirement is required."]
            }));
        }

        var hasInvalidRequirement = requirementList.Any(requirement =>
            requirement is null || requirement.Trim().Length is < 1 or > 200);
        if (hasInvalidRequirement)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] =
                    ["Each vacancy requirement must contain between 1 and 200 characters after trimming."]
            }));
        }

        var hasDuplicateRequirement = requirementList
            .GroupBy(requirement => requirement!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (hasDuplicateRequirement)
        {
            return Result<List<string>>.Failure(VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["requirements"] = ["Vacancy requirements must be unique after trimming and ignoring case."]
            }));
        }

        return requirementList.Select(requirement => requirement!).ToList();
    }
}
