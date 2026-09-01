using hr_sat.Domain;
using hr_sat.Domain.Candidates;

namespace hr_sat.Domain.Vacancies;

public sealed class Vacancy : Entity
{
    private readonly List<VacancyRequirement> _requirements = [];
    private readonly List<Candidate> _candidates = [];

    private Vacancy()
    {
    }

    private Vacancy(string title, DateOnly openedOn)
    {
        Title = title;
        OpenedOn = openedOn;
        Status = VacancyStatus.Open;
    }

    public string Title { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public VacancyStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<VacancyRequirement> Requirements => _requirements;
    public IReadOnlyList<Candidate> Candidates => _candidates;

    public static Result<Vacancy> Create(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements)
    {
        var requirementList = ValidateDefinition(title, openedOn, requirements);
        if (requirementList.IsFailure)
        {
            return Result<Vacancy>.Failure(requirementList.Error);
        }

        var vacancy = new Vacancy(title!, openedOn);
        vacancy.ReplaceRequirements(requirementList.Value);
        return vacancy;
    }

    public Result UpdateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before it can be updated.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        var requirementList = ValidateDefinition(title, openedOn, requirements);
        if (requirementList.IsFailure)
        {
            return requirementList.Error;
        }

        Title = title!;
        OpenedOn = openedOn;
        ReplaceRequirements(requirementList.Value);
        return Result.Success();
    }

    public Result Close(DateTimeOffset closedAt)
    {
        var openResult = EnsureOpen("A closed vacancy must be reopened before it can be closed again.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status != VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a closed vacancy can be reopened."]
            });
        }

        Status = VacancyStatus.Open;
        ClosedAt = null;
        return Result.Success();
    }

    public Result EnsureCanReceiveCandidateImport() =>
        EnsureOpen("A closed vacancy cannot receive candidate imports.");

    public Result EnsureCanRemoveCandidate() =>
        EnsureOpen("A closed vacancy must be reopened before candidates can be removed.");

    private Result EnsureOpen(string message)
    {
        if (Status == VacancyStatus.Closed)
        {
            return VacancyErrors.Invalid(new Dictionary<string, string[]>
            {
                ["status"] = [message]
            });
        }

        return Result.Success();
    }

    private static Result<List<string>> ValidateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements)
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

    private void ReplaceRequirements(IReadOnlyList<string> requirements)
    {
        _requirements.Clear();
        var position = 1;

        foreach (var requirement in requirements)
        {
            _requirements.Add(new VacancyRequirement(requirement, position));
            position++;
        }
    }
}