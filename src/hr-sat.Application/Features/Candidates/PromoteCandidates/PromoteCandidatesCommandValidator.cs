using FluentValidation;

namespace hr_sat.Application.Features.Candidates.PromoteCandidates;

public sealed class PromoteCandidatesCommandValidator
    : AbstractValidator<PromoteCandidatesCommand>
{
    public PromoteCandidatesCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.RoundId).GreaterThan(0);
        RuleFor(command => command.SourceRoundId)
            .GreaterThan(0)
            .NotEqual(command => command.RoundId)
            .WithMessage("Source round must differ from the active round.");
        RuleFor(command => command.CandidateIds)
            .NotNull()
            .NotEmpty();
        RuleForEach(command => command.CandidateIds)
            .GreaterThan(0);
    }
}