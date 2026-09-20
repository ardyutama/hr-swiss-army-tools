using hr_sat.Application.Features.Dispatches.RetryFailed;
using hr_sat.Application.Features.Dispatches.SendToAll;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Dispatches;

public sealed class DispatchValidatorsTests
{
    [Fact]
    public void Validate_Should_RejectNonPositiveIds_ForSendToAll() // US-19
    {
        var result = new SendToAllCommandValidator()
            .Validate(new SendToAllCommand(0, -1));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(SendToAllCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(SendToAllCommand.RoundId));
    }

    [Fact]
    public void Validate_Should_AcceptPositiveIds_ForSendToAll() // US-19
    {
        var result = new SendToAllCommandValidator()
            .Validate(new SendToAllCommand(1, 1));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Should_RejectNonPositiveIds_ForRetryFailed() // domain: Dispatch Run
    {
        var result = new RetryFailedDispatchesCommandValidator()
            .Validate(new RetryFailedDispatchesCommand(0, -1, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(RetryFailedDispatchesCommand.VacancyId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(RetryFailedDispatchesCommand.RoundId));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(RetryFailedDispatchesCommand.RunId));
    }

    [Fact]
    public void Validate_Should_AcceptPositiveIds_ForRetryFailed() // domain: Dispatch Run
    {
        var result = new RetryFailedDispatchesCommandValidator()
            .Validate(new RetryFailedDispatchesCommand(1, 1, 1));

        result.IsValid.ShouldBeTrue();
    }
}
