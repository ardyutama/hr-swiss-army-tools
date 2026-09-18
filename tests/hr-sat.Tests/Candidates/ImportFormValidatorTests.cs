using hr_sat.Application.Features.Candidates.ImportForm;
using hr_sat.Domain.Vacancies;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class ImportFormValidatorTests
{
    [Fact]
    public void Validate_Should_RejectNonPositiveIdsAndMissingFile()
    {
        var result = new ImportFormCommandValidator()
            .Validate(new ImportFormCommand(0, 0, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportFormCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportFormCommand.RoundId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportFormCommand.File));
    }

    [Fact]
    public void Validate_Should_RejectNonCsvExtension()
    {
        var result = ValidateFile("responses.txt", "text/csv", 10);

        result.IsValid.ShouldBeFalse();
        result.Errors
            .Where(error => error.PropertyName == nameof(ImportFormCommand.File))
            .Select(error => error.ErrorMessage)
            .ShouldContain("Only files with the .csv extension can be imported.");
    }

    [Fact]
    public void Validate_Should_RejectEmptyAndOversizedFiles()
    {
        var emptyResult = ValidateFile("responses.csv", "text/csv", 0);
        var oversizedResult = ValidateFile("responses.csv", "text/csv", 25 * 1024 * 1024 + 1);

        emptyResult.Errors
            .Select(error => error.ErrorMessage)
            .ShouldContain("The .csv file must not be empty.");
        oversizedResult.Errors
            .Select(error => error.ErrorMessage)
            .ShouldContain("The .csv file must be 25 MB or smaller.");
    }

    [Fact]
    public void Validate_Should_RejectUnsupportedContentType()
    {
        var result = ValidateFile("responses.csv", "text/html", 10);

        result.IsValid.ShouldBeFalse();
        result.Errors
            .Select(error => error.ErrorMessage)
            .ShouldContain("The uploaded file has an unsupported content type.");
    }

    [Fact]
    public void Validate_Should_AcceptCsvWithMultipartParameters()
    {
        var result = ValidateFile("responses.CSV", "text/csv; charset=utf-8", 10);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_RejectReservedTimestampOrdinalInLayoutPayload()
    {
        var result = new ImportFormCommandValidator().Validate(
            new ImportFormCommand(
                1,
                1,
                new ImportFormFile("responses.csv", "text/csv", 1, Stream.Null),
                new FormLayoutDefinition([
                    new FormLayoutColumn(0, FormLayoutRole.Name, null),
                    new FormLayoutColumn(1, FormLayoutRole.ContactEmail, null)
                ])));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(message => message.Contains("Timestamp", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Should_RejectLayoutPayloadAndDriftConfirmationTogether()
    {
        var result = new ImportFormCommandValidator().Validate(
            new ImportFormCommand(
                1,
                1,
                new ImportFormFile("responses.csv", "text/csv", 1, Stream.Null),
                new FormLayoutDefinition([
                    new FormLayoutColumn(1, FormLayoutRole.Name, null),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]),
                ConfirmDrift: true));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(message => message.Contains("cannot be sent together", StringComparison.Ordinal));
    }

    private static FluentValidation.Results.ValidationResult ValidateFile(
        string fileName,
        string contentType,
        long length) =>
        new ImportFormCommandValidator().Validate(
            new ImportFormCommand(
                1,
                1,
                new ImportFormFile(fileName, contentType, length, Stream.Null)));
}