using hr_sat.Application.Features.Candidates.Delete;
using hr_sat.Application.Features.Candidates.Import;
using hr_sat.Application.Features.Candidates.UpdateDetails;
using hr_sat.Application.Features.Candidates.UpdateNotes;
using hr_sat.Application.Features.Candidates.UpdateOutcome;
using hr_sat.Application.Features.Candidates.UpdateReview;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateValidatorsTests
{
    [Fact]
    public void US_14_Delete_candidate_requires_a_positive_vacancy_id()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(0, 1, 1));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteCandidateCommand.VacancyId));
    }

    [Fact]
    public void US_14_Delete_candidate_requires_a_positive_candidate_id()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(1, 1, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteCandidateCommand.CandidateId));
    }

    [Fact]
    public void US_14_Delete_candidate_accepts_positive_ids()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(1, 1, 2));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void US_12_Import_candidates_requires_a_positive_vacancy_id()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(0, 1, []));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportCandidatesCommand.VacancyId));
    }

    [Fact]
    public void US_12_Import_candidates_rejects_a_null_file_collection()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(1, 1, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportCandidatesCommand.Files));
    }

    [Fact]
    public void US_12_Import_candidates_rejects_an_empty_file_collection()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(1, 1, []));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportCandidatesCommand.Files));
    }

    [Fact]
    public void US_12_Import_candidates_accepts_a_file_collection_with_a_file()
    {
        using var content = new MemoryStream([1]);
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(
                1,
                1,
                [new ImportCandidateFile("candidate.eml", "message/rfc822", 1, content)]));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void US_17_update_candidate_details_rejects_invalid_email_syntax()
    {
        var result = new UpdateCandidateDetailsCommandValidator()
            .Validate(new UpdateCandidateDetailsCommand(1, 1, 1, "Candidate", "not-an-email"));

        result.IsValid.ShouldBeFalse();
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateDetailsCommand.ContactEmail))
            .ErrorMessage
            .ShouldBe("Email must be a valid email address.");
    }

    [Fact]
    public void US_17_update_candidate_details_enforces_command_length_limits()
    {
        var result = new UpdateCandidateDetailsCommandValidator()
            .Validate(new UpdateCandidateDetailsCommand(
                1,
                1,
                1,
                new string('n', 301),
                new string('e', 310) + "@example.com"));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpdateCandidateDetailsCommand.FullName));
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(UpdateCandidateDetailsCommand.ContactEmail));
    }

    [Fact]
    public void US_17_update_candidate_notes_rejects_raw_notes_over_4000_characters()
    {
        var result = new UpdateCandidateNotesCommandValidator()
            .Validate(new UpdateCandidateNotesCommand(1, 1, 1, new string('x', 4001)));

        result.IsValid.ShouldBeFalse();
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateNotesCommand.Notes))
            .ErrorMessage
            .ShouldBe("Notes must be 4000 characters or fewer.");
    }

    [Fact]
    public void US_17_update_candidate_outcome_validates_enum_input_and_raw_note_length()
    {
        var result = new UpdateCandidateOutcomeCommandValidator()
            .Validate(new UpdateCandidateOutcomeCommand(
                1,
                1,
                1,
                "not-an-outcome",
                new string('x', 4001)));

        result.IsValid.ShouldBeFalse();
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateOutcomeCommand.Outcome))
            .ErrorMessage
            .ShouldBe("Hire outcome must be none, hired, runaway, or declined.");
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateOutcomeCommand.Note))
            .ErrorMessage
            .ShouldBe("Notes must be 4000 characters or fewer.");
    }

    [Fact]
    public void US_17_update_candidate_review_validates_enum_input_and_raw_note_length()
    {
        var result = new UpdateCandidateReviewCommandValidator()
            .Validate(new UpdateCandidateReviewCommand(
                1,
                1,
                1,
                "new",
                new string('x', 4001)));

        result.IsValid.ShouldBeFalse();
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateReviewCommand.ReviewStatus))
            .ErrorMessage
            .ShouldBe("Review status must be shortlisted, flagged, or rejected.");
        result.Errors.Single(error =>
                error.PropertyName == nameof(UpdateCandidateReviewCommand.Notes))
            .ErrorMessage
            .ShouldBe("Notes must be 4000 characters or fewer.");
    }
}