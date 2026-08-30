namespace hr_sat.Server.Domain.Vacancies;

public sealed class Vacancy
{
    private readonly List<VacancyRequirement> _requirements = [];

    private Vacancy()
    {
    }

    private Vacancy(string title, DateOnly openedOn)
    {
        Title = title;
        OpenedOn = openedOn;
        Status = VacancyStatus.Open;
    }

    public long Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public VacancyStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<VacancyRequirement> Requirements => _requirements;

    public static Vacancy Create(string? title, DateOnly openedOn, IEnumerable<string?>? requirements)
    {
        var requirementList = ValidateDefinition(title, openedOn, requirements);
        var vacancy = new Vacancy(title!, openedOn);
        vacancy.ReplaceRequirements(requirementList);
        return vacancy;
    }

    public void UpdateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements)
    {
        EnsureOpen("A closed vacancy must be reopened before it can be updated.");
        var requirementList = ValidateDefinition(title, openedOn, requirements);
        Title = title!;
        OpenedOn = openedOn;
        ReplaceRequirements(requirementList);
    }

    public void Close(DateTimeOffset closedAt)
    {
        EnsureOpen("A closed vacancy must be reopened before it can be closed again.");
        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
    }

    public void Reopen()
    {
        if (Status != VacancyStatus.Closed)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a closed vacancy can be reopened."]
            });
        }

        Status = VacancyStatus.Open;
        ClosedAt = null;
    }

    private void EnsureOpen(string message)
    {
        if (Status == VacancyStatus.Closed)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["status"] = [message]
            });
        }
    }

    private static List<string> ValidateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements)
    {
        if (title is null || title.Trim().Length is < 1 or > 200)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["title"] = ["Title must contain between 1 and 200 characters after trimming."]
            });
        }

        if (openedOn == default)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["openedOn"] = ["Opening Date is required."]
            });
        }

        var requirementList = requirements?.ToList() ?? [];
        if (requirementList.Count == 0)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["requirements"] = ["At least one vacancy requirement is required."]
            });
        }

        var hasInvalidRequirement = requirementList.Any(requirement =>
            requirement is null || requirement.Trim().Length is < 1 or > 200);
        if (hasInvalidRequirement)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["requirements"] =
                    ["Each vacancy requirement must contain between 1 and 200 characters after trimming."]
            });
        }

        var hasDuplicateRequirement = requirementList
            .GroupBy(requirement => requirement!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (hasDuplicateRequirement)
        {
            throw new VacancyValidationException(new Dictionary<string, string[]>
            {
                ["requirements"] = ["Vacancy requirements must be unique after trimming and ignoring case."]
            });
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