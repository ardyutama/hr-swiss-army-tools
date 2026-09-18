using FluentValidation;

namespace hr_sat.Application.Features.Candidates.ImportForm;

public sealed class ImportFormCommandValidator : AbstractValidator<ImportFormCommand>
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024;

    private static readonly string[] AcceptedContentTypes =
    [
        "application/csv",
        "application/octet-stream",
        "text/csv",
        "text/plain"
    ];

    public ImportFormCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
        RuleFor(command => command.File)
            .NotNull()
            .WithMessage("A .csv file is required.");
        RuleFor(command => command.File)
            .Must(file => file is not null &&
                file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only files with the .csv extension can be imported.")
            .When(command => command.File is not null);
        RuleFor(command => command.File)
            .Must(file => file is not null && file.Length > 0)
            .WithMessage("The .csv file must not be empty.")
            .When(command => command.File is not null);
        RuleFor(command => command.File)
            .Must(file => file is not null && file.Length <= MaxFileSizeBytes)
            .WithMessage("The .csv file must be 25 MB or smaller.")
            .When(command => command.File is not null);
        RuleFor(command => command.File)
            .Must(file => file is not null && IsAcceptedContentType(file.ContentType))
            .WithMessage("The uploaded file has an unsupported content type.")
            .When(command => command.File is not null);
        RuleFor(command => command)
            .Must(command => !(command.ConfirmDrift && command.Layout is not null))
            .WithMessage("A Form Layout payload and drift confirmation cannot be sent together.");
        When(command => command.Layout is not null, () =>
        {
            RuleFor(command => command.Layout!.Columns)
                .NotNull()
                .WithMessage("Form Layout columns are required.");
            RuleForEach(command => command.Layout!.Columns)
                .ChildRules(column =>
                {
                    column.RuleFor(item => item.Ordinal)
                        .GreaterThan(0)
                        .WithMessage("Column ordinals must be greater than zero because Timestamp is reserved.");
                    column.RuleFor(item => item.Label)
                        .MaximumLength(40)
                        .WithMessage("Column labels must be 40 characters or fewer.");
                    column.RuleFor(item => item.Label)
                        .Must(label => label is null || label == label.Trim())
                        .WithMessage("Column labels must be trimmed.");
                    column.RuleFor(item => item.Label)
                        .Must(label => label is null ||
                            (!label.Contains('\r') && !label.Contains('\n')))
                        .WithMessage("Column labels must be single line.");
                });
        });
    }

    private static bool IsAcceptedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return AcceptedContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);
    }
}