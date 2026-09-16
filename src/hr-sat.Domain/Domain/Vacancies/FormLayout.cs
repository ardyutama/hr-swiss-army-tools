using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public sealed class FormLayout : Entity
{
    private FormLayout()
    {
    }

    private FormLayout(long vacancyId, FormLayoutDefinition definition)
    {
        VacancyId = vacancyId;
        ReplaceValues(definition);
    }

    public long VacancyId { get; private set; }
    public string[] HeaderSnapshot { get; private set; } = [];
    public int? NameColumnOrdinal { get; private set; }
    public int? ContactEmailColumnOrdinal { get; private set; }
    public int? ContactPhoneColumnOrdinal { get; private set; }
    public int? CvLinkColumnOrdinal { get; private set; }
    public bool IsValid =>
        NameColumnOrdinal.HasValue && ContactEmailColumnOrdinal.HasValue;

    internal static Result<FormLayout> Create(
        long vacancyId,
        FormLayoutDefinition definition)
    {
        var validationResult = Validate(vacancyId, definition);
        return validationResult.IsFailure
            ? Result<FormLayout>.Failure(validationResult.Error)
            : new FormLayout(vacancyId, definition);
    }

    internal Result Replace(FormLayoutDefinition definition)
    {
        var validationResult = Validate(VacancyId, definition);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        ReplaceValues(definition);
        return Result.Success();
    }

    private static Result Validate(long vacancyId, FormLayoutDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new Dictionary<string, string[]>();
        if (vacancyId <= 0)
        {
            errors[nameof(VacancyId)] = ["Vacancy is required."];
        }

        if (definition.HeaderSnapshot is null || definition.HeaderSnapshot.Count == 0)
        {
            errors[nameof(FormLayoutDefinition.HeaderSnapshot)] =
                ["The form header snapshot must contain at least one column."];
        }
        else
        {
            ValidateOrdinal(
                definition.NameColumnOrdinal,
                definition.HeaderSnapshot.Count,
                nameof(FormLayoutDefinition.NameColumnOrdinal),
                required: true,
                errors);
            ValidateOrdinal(
                definition.ContactEmailColumnOrdinal,
                definition.HeaderSnapshot.Count,
                nameof(FormLayoutDefinition.ContactEmailColumnOrdinal),
                required: true,
                errors);
            ValidateOrdinal(
                definition.ContactPhoneColumnOrdinal,
                definition.HeaderSnapshot.Count,
                nameof(FormLayoutDefinition.ContactPhoneColumnOrdinal),
                required: false,
                errors);
            ValidateOrdinal(
                definition.CvLinkColumnOrdinal,
                definition.HeaderSnapshot.Count,
                nameof(FormLayoutDefinition.CvLinkColumnOrdinal),
                required: false,
                errors);

            var boundOrdinals = new[]
            {
                definition.NameColumnOrdinal,
                definition.ContactEmailColumnOrdinal,
                definition.ContactPhoneColumnOrdinal,
                definition.CvLinkColumnOrdinal
            }
                .Where(ordinal => ordinal.HasValue)
                .Select(ordinal => ordinal!.Value)
                .ToArray();
            if (boundOrdinals.Length != boundOrdinals.Distinct().Count())
            {
                errors["roles"] = ["Each Form Layout role must use a different column."];
            }
        }

        return errors.Count == 0 ? Result.Success() : FormLayoutErrors.Invalid(errors);
    }

    private static void ValidateOrdinal(
        int? ordinal,
        int headerCount,
        string propertyName,
        bool required,
        IDictionary<string, string[]> errors)
    {
        if (!ordinal.HasValue)
        {
            if (required)
            {
                errors[propertyName] = ["This Form Layout role must be bound."];
            }

            return;
        }

        if (ordinal.Value <= 0 || ordinal.Value >= headerCount)
        {
            errors[propertyName] =
                ["The role must reference a non-timestamp column in the header snapshot."];
        }
    }

    private void ReplaceValues(FormLayoutDefinition definition)
    {
        HeaderSnapshot = definition.HeaderSnapshot!.ToArray();
        NameColumnOrdinal = definition.NameColumnOrdinal;
        ContactEmailColumnOrdinal = definition.ContactEmailColumnOrdinal;
        ContactPhoneColumnOrdinal = definition.ContactPhoneColumnOrdinal;
        CvLinkColumnOrdinal = definition.CvLinkColumnOrdinal;
    }
}