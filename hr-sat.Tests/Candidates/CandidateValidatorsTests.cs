using hr_sat.Application.Features.Candidates.Delete;
using hr_sat.Application.Features.Candidates.Import;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateValidatorsTests
{
    [Fact]
    public void US_14_Delete_candidate_requires_a_positive_vacancy_id()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(0, 1));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteCandidateCommand.VacancyId));
    }

    [Fact]
    public void US_14_Delete_candidate_requires_a_positive_candidate_id()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(1, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(DeleteCandidateCommand.CandidateId));
    }

    [Fact]
    public void US_14_Delete_candidate_accepts_positive_ids()
    {
        var result = new DeleteCandidateCommandValidator()
            .Validate(new DeleteCandidateCommand(1, 2));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void US_12_Import_candidates_requires_a_positive_vacancy_id()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(0, []));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportCandidatesCommand.VacancyId));
    }

    [Fact]
    public void US_12_Import_candidates_rejects_a_null_file_collection()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(1, null));

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName)
            .ShouldContain(nameof(ImportCandidatesCommand.Files));
    }

    [Fact]
    public void US_12_Import_candidates_rejects_an_empty_file_collection()
    {
        var result = new ImportCandidatesCommandValidator()
            .Validate(new ImportCandidatesCommand(1, []));

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
                [new ImportCandidateFile("candidate.eml", "message/rfc822", 1, content)]));

        result.IsValid.ShouldBeTrue();
    }
}