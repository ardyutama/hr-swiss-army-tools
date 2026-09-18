using hr_sat.Application.Features.FormLayouts.Upsert;
using hr_sat.Domain.Vacancies;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutValidatorTests
{
    [Fact]
    public void Validate_Should_RejectNonPositiveVacancyIdAndMissingHeaders()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(0, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertFormLayoutCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertFormLayoutCommand.Columns));
    }

    [Fact]
    public void Validate_Should_AcceptPositiveVacancyIdAndColumns()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, null),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_RejectReservedTimestampOrdinal()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                [
                    new FormLayoutColumn(0, FormLayoutRole.Name, null),
                    new FormLayoutColumn(1, FormLayoutRole.ContactEmail, null)
                ]));

        result.IsValid.ShouldBeFalse();
        result.Errors
            .Single(error => error.PropertyName == "Columns[0].Ordinal")
            .ErrorMessage
            .ShouldContain("Timestamp");
    }

    [Fact]
    public void Validate_Should_RejectUntrimmedLabel()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, " Applicant "),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain("Column labels must be trimmed.");
    }

    [Fact]
    public void Validate_Should_RejectMultilineLabel()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, "Applicant\nname"),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain("Column labels must be single line.");
    }

    [Fact]
    public void Validate_Should_RejectLabelLongerThanFortyCharacters()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                [
                    new FormLayoutColumn(1, FormLayoutRole.Name, new string('x', 41)),
                    new FormLayoutColumn(2, FormLayoutRole.ContactEmail, null)
                ]));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain("Column labels must be 40 characters or fewer.");
    }
}
