using hr_sat.Application.Features.FormLayouts.Upsert;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.FormLayouts;

public sealed class FormLayoutValidatorTests
{
    [Fact]
    public void Validate_Should_RejectNonPositiveVacancyIdAndMissingHeaders()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(0, null, null, null, null, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertFormLayoutCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpsertFormLayoutCommand.HeaderSnapshot));
    }

    [Fact]
    public void Validate_Should_AcceptPositiveVacancyIdAndHeaderSnapshot()
    {
        var result = new UpsertFormLayoutCommandValidator().Validate(
            new UpsertFormLayoutCommand(
                1,
                ["Timestamp", "Name", "Email"],
                1,
                2,
                null,
                null));

        result.IsValid.ShouldBeTrue();
    }
}
