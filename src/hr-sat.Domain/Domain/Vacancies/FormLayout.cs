using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public sealed class FormLayout : Entity
{
    private FormLayout()
    {
    }

    private FormLayout(
        long vacancyId,
        IReadOnlyList<string> headerSnapshot,
        FormLayoutDefinition definition)
    {
        VacancyId = vacancyId;
        ReplaceValues(headerSnapshot, definition);
    }

    public long VacancyId { get; private set; }
    public string[] HeaderSnapshot { get; private set; } = [];
    public IReadOnlyList<FormLayoutColumn> Columns { get; private set; } = [];
    public int? NameColumnOrdinal => GetColumnOrdinal(FormLayoutRole.Name);
    public int? ContactEmailColumnOrdinal => GetColumnOrdinal(FormLayoutRole.ContactEmail);
    public int? ContactPhoneColumnOrdinal => GetColumnOrdinal(FormLayoutRole.ContactPhone);
    public int? CvLinkColumnOrdinal => GetColumnOrdinal(FormLayoutRole.CvLink);
    public bool IsValid =>
        HeaderSnapshot.Length > 0 &&
        NameColumnOrdinal.HasValue &&
        ContactEmailColumnOrdinal.HasValue;

    internal static Result<FormLayout> Create(
        long vacancyId,
        IReadOnlyList<string>? headerSnapshot,
        FormLayoutDefinition definition)
    {
        var validationResult = Validate(vacancyId, headerSnapshot, definition);
        return validationResult.IsFailure
            ? Result<FormLayout>.Failure(validationResult.Error)
            : new FormLayout(vacancyId, headerSnapshot!, definition);
    }

    internal Result Replace(FormLayoutDefinition definition)
    {
        var validationResult = Validate(VacancyId, HeaderSnapshot, definition);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        ReplaceValues(HeaderSnapshot, definition);
        return Result.Success();
    }

    internal Result ReplaceFromImport(
        FormLayoutDefinition definition,
        IReadOnlyList<string>? headerSnapshot)
    {
        var validationResult = Validate(VacancyId, headerSnapshot, definition);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        ReplaceValues(headerSnapshot!, definition);
        return Result.Success();
    }

    public IReadOnlyList<FormLayoutHeaderChange> DetectDrift(
        IReadOnlyList<string>? newHeaders)
    {
        var headers = newHeaders ?? [];
        return Columns
            .OrderBy(column => column.Ordinal)
            .Select(column =>
            {
                var now = column.Ordinal < headers.Count
                    ? headers[column.Ordinal]
                    : "column no longer present";
                return new FormLayoutHeaderChange(
                    column.Ordinal,
                    HeaderSnapshot[column.Ordinal],
                    now);
            })
            .Where(change => !string.Equals(change.Was, change.Now, StringComparison.Ordinal))
            .ToArray();
    }

    internal Result AdoptHeaderSnapshot(IReadOnlyList<string>? headerSnapshot)
    {
        var validationResult = Validate(
            VacancyId,
            headerSnapshot,
            new FormLayoutDefinition(Columns));
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        HeaderSnapshot = headerSnapshot!.ToArray();
        return Result.Success();
    }

    internal int? GetColumnOrdinal(FormLayoutRole role) =>
        Columns.SingleOrDefault(column => column.Role == role)?.Ordinal;

    private static Result Validate(
        long vacancyId,
        IReadOnlyList<string>? headerSnapshot,
        FormLayoutDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new Dictionary<string, string[]>();
        if (vacancyId <= 0)
        {
            errors[nameof(VacancyId)] = ["Vacancy is required."];
        }

        if (headerSnapshot is null || headerSnapshot.Count == 0)
        {
            errors[nameof(headerSnapshot)] =
                ["The form header snapshot must contain at least one column."];
        }
        if (definition.Columns is null)
        {
            errors[nameof(FormLayoutDefinition.Columns)] =
                ["The Form Layout must contain at least the required column bindings."];
        }
        else if (headerSnapshot is not null && headerSnapshot.Count > 0)
        {
            if (definition.Columns.Count > 8)
            {
                errors[nameof(FormLayoutDefinition.Columns)] =
                    ["A Form Layout can pick at most 8 columns, including role bindings."];
            }

            var duplicateOrdinals = definition.Columns
                .GroupBy(column => column.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateOrdinals.Length > 0)
            {
                errors["ordinals"] = ["Each picked Form Layout column must use a different ordinal."];
            }

            var duplicateRoles = definition.Columns
                .Where(column => column.Role.HasValue)
                .GroupBy(column => column.Role!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateRoles.Length > 0)
            {
                errors["roles"] = ["Each Form Layout role must use a different column."];
            }

            foreach (var column in definition.Columns)
            {
                if (column.Ordinal <= 0 || column.Ordinal >= headerSnapshot.Count)
                {
                    errors[$"columns[{column.Ordinal}]"] =
                        ["A picked Form Layout column must use an ordinal from 1 through the last header ordinal; ordinal 0 is reserved for Timestamp."];
                }
            }

            if (!definition.Columns.Any(column => column.Role == FormLayoutRole.Name))
            {
                errors["name"] = ["The Name role must be bound."];
            }

            if (!definition.Columns.Any(column => column.Role == FormLayoutRole.ContactEmail))
            {
                errors["contactEmail"] = ["The Contact Email role must be bound."];
            }
        }

        return errors.Count == 0 ? Result.Success() : FormLayoutErrors.Invalid(errors);
    }

    private void ReplaceValues(
        IReadOnlyList<string> headerSnapshot,
        FormLayoutDefinition definition)
    {
        HeaderSnapshot = headerSnapshot.ToArray();
        Columns = definition.Columns!
            .Select(column => column with
            {
                Label = string.IsNullOrWhiteSpace(column.Label)
                    ? null
                    : column.Label.Trim()
            })
            .ToArray();
    }
}