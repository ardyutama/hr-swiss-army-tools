namespace hr_sat.Server.Domain.Candidates;

public sealed class Candidate
{
    private readonly List<CvDocument> _cvDocuments = [];

    private Candidate()
    {
    }

    private Candidate(
        long vacancyId,
        string? sourceSenderName,
        string? sourceSenderEmail,
        string? sourceSubject,
        string? sourceBodyText,
        DateTimeOffset? sourceSentAt,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt)
    {
        VacancyId = vacancyId;
        ReviewStatus = CandidateReviewStatus.New;
        ExtractionStatus = CandidateExtractionStatus.Pending;
        SourceSenderName = sourceSenderName;
        SourceSenderEmail = sourceSenderEmail;
        SourceSubject = sourceSubject;
        SourceBodyText = sourceBodyText;
        SourceSentAt = sourceSentAt;
        SourceOriginalFilename = sourceOriginalFilename;
        SourceStorageKey = sourceStorageKey;
        SourceSizeBytes = sourceSizeBytes;
        SourceSha256 = sourceSha256.ToArray();
        ImportedAt = importedAt;
    }

    public long Id { get; private set; }
    public long VacancyId { get; private set; }
    public CandidateReviewStatus ReviewStatus { get; private set; }
    public CandidateExtractionStatus ExtractionStatus { get; private set; }
    public string? FullName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Notes { get; private set; }
    public string? SourceSenderName { get; private set; }
    public string? SourceSenderEmail { get; private set; }
    public string? SourceSubject { get; private set; }
    public string? SourceBodyText { get; private set; }
    public DateTimeOffset? SourceSentAt { get; private set; }
    public string SourceOriginalFilename { get; private set; } = string.Empty;
    public string SourceStorageKey { get; private set; } = string.Empty;
    public long SourceSizeBytes { get; private set; }
    public byte[] SourceSha256 { get; private set; } = [];
    public DateTimeOffset ImportedAt { get; private set; }
    public IReadOnlyList<CvDocument> CvDocuments => _cvDocuments;

    internal static Candidate Import(
        long vacancyId,
        string? sourceSenderName,
        string? sourceSenderEmail,
        string? sourceSubject,
        string? sourceBodyText,
        DateTimeOffset? sourceSentAt,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt,
        IReadOnlyList<StoredCvDocument> cvDocuments)
    {
        ValidateSource(
            vacancyId,
            sourceOriginalFilename,
            sourceStorageKey,
            sourceSizeBytes,
            sourceSha256,
            importedAt,
            sourceSenderName,
            sourceSenderEmail);

        if (cvDocuments.Count == 0)
        {
            throw new CandidateValidationException(new Dictionary<string, string[]>
            {
                ["documents"] = ["At least one CV document is required."]
            });
        }

        var hasDuplicatePosition = cvDocuments
            .GroupBy(document => document.Position)
            .Any(group => group.Count() > 1);
        if (hasDuplicatePosition)
        {
            throw new CandidateValidationException(new Dictionary<string, string[]>
            {
                ["documents"] = ["CV document positions must be unique."]
            });
        }

        var candidate = new Candidate(
            vacancyId,
            sourceSenderName,
            sourceSenderEmail,
            sourceSubject,
            sourceBodyText,
            sourceSentAt,
            sourceOriginalFilename,
            sourceStorageKey,
            sourceSizeBytes,
            sourceSha256,
            importedAt);

        foreach (var document in cvDocuments.OrderBy(document => document.Position))
        {
            ValidateDocument(document);
            candidate._cvDocuments.Add(new CvDocument(
                document.OriginalFilename,
                document.StorageKey,
                document.Position,
                document.IsPrimary,
                document.SizeBytes,
                document.Sha256));
        }

        if (cvDocuments.Count == 1 && !cvDocuments[0].IsPrimary)
        {
            throw new CandidateValidationException(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate with one CV document must have a primary document."]
            });
        }

        if (cvDocuments.Count(document => document.IsPrimary) > 1)
        {
            throw new CandidateValidationException(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate can have at most one primary CV document."]
            });
        }

        return candidate;
    }

    private static void ValidateSource(
        long vacancyId,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt,
        string? sourceSenderName,
        string? sourceSenderEmail)
    {
        var errors = new Dictionary<string, string[]>();
        if (vacancyId <= 0)
        {
            errors["vacancyId"] = ["Vacancy is required."];
        }

        if (string.IsNullOrWhiteSpace(sourceOriginalFilename))
        {
            errors["sourceOriginalFilename"] = ["The source filename is required."];
        }

        if (string.IsNullOrWhiteSpace(sourceStorageKey))
        {
            errors["sourceStorageKey"] = ["The source storage key is required."];
        }

        if (sourceSizeBytes <= 0)
        {
            errors["sourceSizeBytes"] = ["The source email must not be empty."];
        }

        if (sourceSha256.Length != 32)
        {
            errors["sourceSha256"] = ["The source hash must be a SHA-256 hash."];
        }

        if (importedAt == default)
        {
            errors["importedAt"] = ["The import timestamp is required."];
        }

        ValidateOptionalText(errors, "sourceSenderName", sourceSenderName, 300);
        ValidateOptionalText(errors, "sourceSenderEmail", sourceSenderEmail, 320);

        if (errors.Count > 0)
        {
            throw new CandidateValidationException(errors);
        }
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

    private static void ValidateDocument(StoredCvDocument document)
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

        if (errors.Count > 0)
        {
            throw new CandidateValidationException(errors);
        }
    }
}

internal sealed record StoredCvDocument(
    string OriginalFilename,
    string StorageKey,
    int Position,
    bool IsPrimary,
    long SizeBytes,
    byte[] Sha256);