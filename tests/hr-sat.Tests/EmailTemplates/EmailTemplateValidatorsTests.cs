using hr_sat.Application.Features.EmailTemplates.Delete;
using hr_sat.Application.Features.EmailTemplates.Upsert;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.EmailTemplates;

public sealed class EmailTemplateValidatorsTests
{
    [Fact]
    public void Upsert_Should_RejectInvalidKind()
    {
        var result = new UpsertEmailTemplateCommandValidator().Validate(
            new UpsertEmailTemplateCommand(1, "other", "Subject", "Body"));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertEmailTemplateCommand.Kind));
    }

    [Fact]
    public void Upsert_Should_RejectBlankBodyAndOverlongSubject()
    {
        var result = new UpsertEmailTemplateCommandValidator().Validate(
            new UpsertEmailTemplateCommand(
                1,
                "shortlisted",
                new string('x', 999),
                "   "));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertEmailTemplateCommand.Subject));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertEmailTemplateCommand.Body));
    }

    [Fact]
    public void Upsert_Should_AcceptBothTemplateKindsAndValidContent()
    {
        var result = new UpsertEmailTemplateCommandValidator().Validate(
            new UpsertEmailTemplateCommand(
                1,
                "rejected",
                "Subject",
                "Body"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Delete_Should_RequirePositiveVacancyIdAndKnownKind()
    {
        var result = new DeleteEmailTemplateCommandValidator().Validate(
            new DeleteEmailTemplateCommand(0, "other"));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteEmailTemplateCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteEmailTemplateCommand.Kind));
    }

    [Fact]
    public void Delete_Should_AcceptKnownKindAndPositiveVacancyId()
    {
        var result = new DeleteEmailTemplateCommandValidator().Validate(
            new DeleteEmailTemplateCommand(1, "shortlisted"));

        result.IsValid.ShouldBeTrue();
    }
}