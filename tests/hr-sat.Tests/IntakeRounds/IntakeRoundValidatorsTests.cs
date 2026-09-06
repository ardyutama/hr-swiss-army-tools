using hr_sat.Application.Features.IntakeRounds.Close;
using hr_sat.Application.Features.IntakeRounds.Create;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.IntakeRounds;

public sealed class IntakeRoundValidatorsTests
{
    [Fact]
    public void Create_Should_RequirePositiveVacancyId()
    {
        var result = new CreateIntakeRoundCommandValidator()
            .Validate(new CreateIntakeRoundCommand(0, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(CreateIntakeRoundCommand.VacancyId));
    }

    [Fact]
    public void Create_Should_RejectNamesLongerThan200Characters()
    {
        var result = new CreateIntakeRoundCommandValidator()
            .Validate(new CreateIntakeRoundCommand(1, new string('x', 201)));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(CreateIntakeRoundCommand.Name));
    }

    [Fact]
    public void Create_Should_AcceptPositiveVacancyIdAndOptionalName()
    {
        var result = new CreateIntakeRoundCommandValidator()
            .Validate(new CreateIntakeRoundCommand(1, "Second wave"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Close_Should_RequirePositiveVacancyId()
    {
        var result = new CloseIntakeRoundCommandValidator()
            .Validate(new CloseIntakeRoundCommand(0, 1));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(CloseIntakeRoundCommand.VacancyId));
    }

    [Fact]
    public void Close_Should_RequirePositiveRoundId()
    {
        var result = new CloseIntakeRoundCommandValidator()
            .Validate(new CloseIntakeRoundCommand(1, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(CloseIntakeRoundCommand.RoundId));
    }

    [Fact]
    public void Close_Should_AcceptPositiveIds()
    {
        var result = new CloseIntakeRoundCommandValidator()
            .Validate(new CloseIntakeRoundCommand(1, 2));

        result.IsValid.ShouldBeTrue();
    }
}
