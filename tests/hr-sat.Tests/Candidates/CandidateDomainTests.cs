using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Candidates;

public sealed class CandidateDomainTests
{
    [Fact]
    public void Domain_email_only_candidate_is_valid()
    {
        var result = Candidate.Import(new CandidateImportData(
            1,
            "Candidate Applicant",
            "candidate@example.com",
            "Candidate application",
            "Please find my CV attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            "candidate.eml",
            "source-emails/candidate.eml",
            100,
            new byte[32],
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            []));

        result.IsSuccess.ShouldBeTrue();
        result.Value.CvDocuments.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_candidate_details_are_trimmed_when_valid()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        var result = candidate.UpdateDetails(
            "  Candidate Applicant  ",
            "  candidate@example.com  ");

        result.IsSuccess.ShouldBeTrue();
        candidate.FullName.ShouldBe("Candidate Applicant");
        candidate.ContactEmail.ShouldBe("candidate@example.com");
    }

    [Fact]
    public void Domain_candidate_details_require_trimmed_values_within_length_limits()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        var result = candidate.UpdateDetails(
            new string('n', 301),
            new string('e', 321));

        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors["fullName"]
            .ShouldContain("Name must contain between 1 and 300 characters after trimming.");
        error.Errors["contactEmail"]
            .ShouldContain("Email must contain between 1 and 320 characters after trimming.");
        candidate.FullName.ShouldBeNull();
        candidate.ContactEmail.ShouldBeNull();
    }

    [Fact]
    public void Domain_notes_replace_and_trim_values_and_turn_whitespace_into_null()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        candidate.UpdateNotes("  Existing note  ").IsSuccess.ShouldBeTrue();
        candidate.Notes.ShouldBe("Existing note");

        candidate.UpdateNotes(" \t ").IsSuccess.ShouldBeTrue();
        candidate.Notes.ShouldBeNull();
    }

    [Fact]
    public void Domain_notes_reject_raw_overflow_without_changing_existing_notes()
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        candidate.UpdateNotes("Keep this note.").IsSuccess.ShouldBeTrue();

        var result = candidate.UpdateNotes(new string('x', 4001));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["notes"]
            .ShouldContain("Notes must be 4000 characters or fewer.");
        candidate.Notes.ShouldBe("Keep this note.");
    }

    [Fact]
    public void Domain_review_decisions_replace_notes_and_reject_new_as_a_decision()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        candidate.ApplyReview(CandidateReviewStatus.Flagged, "  Review note  ")
            .IsSuccess.ShouldBeTrue();
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.Flagged);
        candidate.Notes.ShouldBe("Review note");

        candidate.ApplyReview(CandidateReviewStatus.Rejected, " \t ")
            .IsSuccess.ShouldBeTrue();
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.Rejected);
        candidate.Notes.ShouldBeNull();

        var result = candidate.ApplyReview(CandidateReviewStatus.New, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["reviewStatus"]
            .ShouldContain("A review decision must be shortlisted, flagged, or rejected.");
        candidate.ReviewStatus.ShouldBe(CandidateReviewStatus.Rejected);
    }

    [Fact]
    public void Domain_outcome_notes_append_trimmed_values_and_ignore_blank_notes()
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, "Existing review context.")
            .IsSuccess.ShouldBeTrue();

        candidate.SetHireOutcome(CandidateHireOutcome.Hired, "  Started on Monday.  ")
            .IsSuccess.ShouldBeTrue();
        candidate.HireOutcome.ShouldBe(CandidateHireOutcome.Hired);
        candidate.Notes.ShouldBe("Existing review context.\nStarted on Monday.");

        candidate.SetHireOutcome(CandidateHireOutcome.None, " \t ")
            .IsSuccess.ShouldBeTrue();
        candidate.HireOutcome.ShouldBe(CandidateHireOutcome.None);
        candidate.Notes.ShouldBe("Existing review context.\nStarted on Monday.");
    }

    [Fact]
    public void Domain_outcome_note_overflow_preserves_outcome_and_notes_atomically()
    {
        var candidate = CandidateTestData.CreateCandidate(1);
        var existingNotes = new string('x', 3999);
        candidate.ApplyReview(CandidateReviewStatus.Shortlisted, existingNotes)
            .IsSuccess.ShouldBeTrue();

        var result = candidate.SetHireOutcome(CandidateHireOutcome.Declined, " new note ");

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["notes"]
            .ShouldContain("Notes must be 4000 characters or fewer.");
        candidate.HireOutcome.ShouldBe(CandidateHireOutcome.None);
        candidate.Notes.ShouldBe(existingNotes);
    }

    [Fact]
    public void Domain_promotion_requires_an_eligible_candidate_and_valid_details()
    {
        var newCandidate = CandidateTestData.CreateCandidate(1);
        newCandidate.CanBePromoted.ShouldBeTrue();

        var flaggedCandidate = CandidateTestData.CreateCandidate(1);
        flaggedCandidate.ApplyReview(CandidateReviewStatus.Flagged, null)
            .IsSuccess.ShouldBeTrue();
        flaggedCandidate.CanBePromoted.ShouldBeTrue();

        var shortlistedCandidate = CandidateTestData.CreateCandidate(1);
        shortlistedCandidate.ApplyReview(CandidateReviewStatus.Shortlisted, null)
            .IsSuccess.ShouldBeTrue();
        shortlistedCandidate.CanBePromoted.ShouldBeTrue();

        var rejectedCandidate = CandidateTestData.CreateCandidate(1);
        rejectedCandidate.ApplyReview(CandidateReviewStatus.Rejected, null)
            .IsSuccess.ShouldBeTrue();
        rejectedCandidate.CanBePromoted.ShouldBeFalse();

        var hiredCandidate = CandidateTestData.CreateCandidate(1);
        hiredCandidate.ApplyReview(CandidateReviewStatus.Shortlisted, null)
            .IsSuccess.ShouldBeTrue();
        hiredCandidate.SetHireOutcome(CandidateHireOutcome.Hired)
            .IsSuccess.ShouldBeTrue();
        hiredCandidate.CanBePromoted.ShouldBeFalse();

        var invalidDetails = new[]
        {
            (TargetRoundId: 0L, SourceRoundNumber: 1, PromotedAt: new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero)),
            (TargetRoundId: 2L, SourceRoundNumber: 0, PromotedAt: new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero)),
            (TargetRoundId: 2L, SourceRoundNumber: 1, PromotedAt: default(DateTimeOffset))
        };

        foreach (var details in invalidDetails)
        {
            var candidate = CandidateTestData.CreateCandidate(1);
            var result = candidate.PromoteTo(
                details.TargetRoundId,
                details.SourceRoundNumber,
                details.PromotedAt);

            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBeOfType<ValidationError>()
                .Errors["promotion"]
                .ShouldContain("Promotion details are invalid.");
            candidate.IntakeRoundId.ShouldBe(1);
            candidate.PromotedFromRoundNumber.ShouldBeNull();
            candidate.PromotedAt.ShouldBeNull();
        }
    }

    [Fact]
    public void Domain_requirement_reviews_validate_ids_and_upsert_existing_reviews()
    {
        var candidate = CandidateTestData.CreateCandidate(1);

        var invalidResult = candidate.SetRequirementReview(0, true);

        invalidResult.IsFailure.ShouldBeTrue();
        invalidResult.Error.ShouldBeOfType<ValidationError>()
            .Errors["requirementId"]
            .ShouldContain("Vacancy requirement is required.");
        candidate.RequirementReviews.ShouldBeEmpty();

        candidate.SetRequirementReview(42, true).IsSuccess.ShouldBeTrue();
        candidate.SetRequirementReview(42, false).IsSuccess.ShouldBeTrue();

        candidate.RequirementReviews.ShouldHaveSingleItem().Confirmed.ShouldBeFalse();
    }

    [Fact]
    public void Domain_import_orders_documents_by_position()
    {
        var result = Candidate.Import(ValidImportData(
        [
            ValidDocument(2, false),
            ValidDocument(1, true)
        ]));

        result.IsSuccess.ShouldBeTrue();
        result.Value.CvDocuments.Select(document => document.Position)
            .ShouldBe([1, 2]);
    }

    [Fact]
    public void Domain_import_rejects_duplicate_document_positions()
    {
        var result = Candidate.Import(ValidImportData(
        [
            ValidDocument(1, true),
            ValidDocument(1, false)
        ]));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["documents"]
            .ShouldContain("CV document positions must be unique.");
    }

    [Fact]
    public void Domain_import_requires_exactly_one_primary_for_a_single_document_and_at_most_one_overall()
    {
        var missingPrimary = Candidate.Import(ValidImportData([ValidDocument(1, false)]));

        missingPrimary.IsFailure.ShouldBeTrue();
        missingPrimary.Error.ShouldBeOfType<ValidationError>()
            .Errors["documents"]
            .ShouldContain("A candidate with one CV document must have a primary document.");

        var multiplePrimary = Candidate.Import(ValidImportData(
        [
            ValidDocument(1, true),
            ValidDocument(2, true)
        ]));

        multiplePrimary.IsFailure.ShouldBeTrue();
        multiplePrimary.Error.ShouldBeOfType<ValidationError>()
            .Errors["documents"]
            .ShouldContain("A candidate can have at most one primary CV document.");
    }

    [Theory]
    [InlineData("intakeRoundId", "Intake round is required.")]
    [InlineData("sourceOriginalFilename", "The source filename is required.")]
    [InlineData("sourceStorageKey", "The source storage key is required.")]
    [InlineData("sourceSizeBytes", "The source email must not be empty.")]
    [InlineData("sourceSha256", "The source hash must be a SHA-256 hash.")]
    [InlineData("importedAt", "The import timestamp is required.")]
    [InlineData("sourceSenderName", "The value must contain between 1 and 300 characters after trimming.")]
    [InlineData("sourceSenderEmail", "The value must contain between 1 and 320 characters after trimming.")]
    public void Domain_import_rejects_invalid_source_metadata(string field, string expectedMessage)
    {
        var validData = ValidImportData();
        var invalidData = field switch
        {
            "intakeRoundId" => validData with { IntakeRoundId = 0 },
            "sourceOriginalFilename" => validData with { SourceOriginalFilename = " " },
            "sourceStorageKey" => validData with { SourceStorageKey = " " },
            "sourceSizeBytes" => validData with { SourceSizeBytes = 0 },
            "sourceSha256" => validData with { SourceSha256 = new byte[31] },
            "importedAt" => validData with { ImportedAt = default },
            "sourceSenderName" => validData with { SourceSenderName = " " },
            "sourceSenderEmail" => validData with { SourceSenderEmail = new string('e', 321) },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        var result = Candidate.Import(invalidData);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors[field]
            .ShouldContain(expectedMessage);
    }

    [Theory]
    [InlineData("filename", "Each CV document filename is required.")]
    [InlineData("storageKey", "Each CV document storage key is required.")]
    [InlineData("position", "Each CV document position must be positive.")]
    [InlineData("size", "Each CV document must not be empty.")]
    [InlineData("hash", "Each CV document hash must be a SHA-256 hash.")]
    public void Domain_import_rejects_invalid_document_metadata(string field, string expectedMessage)
    {
        var result = Candidate.Import(ValidImportData([InvalidDocument(field)]));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>()
            .Errors["documents"]
            .ShouldContain(expectedMessage);
    }

    private static CandidateImportData ValidImportData(
        IReadOnlyList<StoredCvDocument>? cvDocuments = null) =>
        new(
            1,
            "Candidate Applicant",
            "candidate@example.com",
            "Candidate application",
            "Please find my CV attached.",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero),
            "candidate.eml",
            "source-emails/candidate.eml",
            100,
            new byte[32],
            new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.Zero),
            cvDocuments ?? []);

    private static StoredCvDocument ValidDocument(int position, bool isPrimary) =>
        new(
            $"candidate-{position}.pdf",
            $"cv-documents/candidate-{position}.pdf",
            position,
            isPrimary,
            20,
            new byte[32]);

    private static StoredCvDocument InvalidDocument(string field)
    {
        var validDocument = ValidDocument(1, true);
        return field switch
        {
            "filename" => validDocument with { OriginalFilename = " " },
            "storageKey" => validDocument with { StorageKey = " " },
            "position" => validDocument with { Position = 0 },
            "size" => validDocument with { SizeBytes = 0 },
            "hash" => validDocument with { Sha256 = new byte[31] },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };
    }
}