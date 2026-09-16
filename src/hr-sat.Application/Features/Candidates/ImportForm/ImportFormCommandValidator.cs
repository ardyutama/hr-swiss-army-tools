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