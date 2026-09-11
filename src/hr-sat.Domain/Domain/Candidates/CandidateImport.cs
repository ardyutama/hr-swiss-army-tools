using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

internal static class CandidateImport
{
    public static Result Validate(CandidateImportData importData)
    {
        var sourceResult = ValidateSource(importData);
        if (sourceResult.IsFailure)
        {
            return sourceResult;
        }

        var hasDuplicatePosition = importData.CvDocuments
            .GroupBy(document => document.Position)
            .Any(group => group.Count() > 1);
        if (hasDuplicatePosition)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["CV document positions must be unique."]
            });
        }

        foreach (var document in OrderDocuments(importData.CvDocuments))
        {
            var documentResult = ValidateDocument(document);
            if (documentResult.IsFailure)
            {
                return documentResult;
            }
        }

        if (importData.CvDocuments.Count == 1 && !importData.CvDocuments[0].IsPrimary)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate with one CV document must have a primary document."]
            });
        }

        if (importData.CvDocuments.Count(document => document.IsPrimary) > 1)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate can have at most one primary CV document."]
            });
        }

        return Result.Success();
    }

    public static IEnumerable<StoredCvDocument> OrderDocuments(
        IReadOnlyList<StoredCvDocument> documents) =>
        documents.OrderBy(document => document.Position);

    private static Result ValidateSource(CandidateImportData importData)
    {
        var errors = new Dictionary<string, string[]>();
        if (importData.IntakeRoundId <= 0)
        {
            errors["intakeRoundId"] = ["Intake round is required."];
        }

        if (string.IsNullOrWhiteSpace(importData.SourceOriginalFilename))
        {
            errors["sourceOriginalFilename"] = ["The source filename is required."];
        }

        if (string.IsNullOrWhiteSpace(importData.SourceStorageKey))
        {
            errors["sourceStorageKey"] = ["The source storage key is required."];
        }

        if (importData.SourceSizeBytes <= 0)
        {
            errors["sourceSizeBytes"] = ["The source email must not be empty."];
        }

        if (importData.SourceSha256.Length != 32)
        {
            errors["sourceSha256"] = ["The source hash must be a SHA-256 hash."];
        }

        if (importData.ImportedAt == default)
        {
            errors["importedAt"] = ["The import timestamp is required."];
        }

        ValidateOptionalText(errors, "sourceSenderName", importData.SourceSenderName, 300);
        ValidateOptionalText(errors, "sourceSenderEmail", importData.SourceSenderEmail, 320);

        return errors.Count > 0 ? CandidateErrors.Invalid(errors) : Result.Success();
    }

    private static void ValidateOptionalText(
        IDictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (value is null)
        {
            return;
        }

        var length = value.Trim().Length;
        if (length < 1 || length > maximumLength)
        {
            errors[field] = [$"The value must contain between 1 and {maximumLength} characters after trimming."];
        }
    }

    private static Result ValidateDocument(StoredCvDocument document)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(document.OriginalFilename))
        {
            errors["documents"] = ["Each CV document filename is required."];
        }

        if (string.IsNullOrWhiteSpace(document.StorageKey))
        {
            errors["documents"] = ["Each CV document storage key is required."];
        }

        if (document.Position <= 0)
        {
            errors["documents"] = ["Each CV document position must be positive."];
        }

        if (document.SizeBytes <= 0)
        {
            errors["documents"] = ["Each CV document must not be empty."];
        }

        if (document.Sha256.Length != 32)
        {
            errors["documents"] = ["Each CV document hash must be a SHA-256 hash."];
        }

        return errors.Count > 0 ? CandidateErrors.Invalid(errors) : Result.Success();
    }
}