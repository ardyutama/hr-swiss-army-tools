using FluentValidation;

namespace hr_sat.Application.Features.Candidates.UpdateDetails;

public sealed class UpdateCandidateDetailsCommandValidator
    : AbstractValidator<UpdateCandidateDetailsCommand>
{
    public UpdateCandidateDetailsCommandValidator()
    {
        RuleFor(command => command.VacancyId).GreaterThan(0);
        RuleFor(command => command.CandidateId).GreaterThan(0);
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(300);
        RuleFor(command => command.ContactEmail)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Email is required.")
            .MaximumLength(320)
            .EmailAddress()
            .WithMessage("Email must be a valid email address.");
    }
}
