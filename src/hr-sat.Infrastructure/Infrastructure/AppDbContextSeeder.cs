using System.Text;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Infrastructure;

public static class AppDbContextSeeder
{
    private const string FieldTechnicianTitle = "Field Technician — Surabaya";
    private const string WarehouseSupervisorTitle = "Warehouse Supervisor — Bekasi";
    private const string PeopleOperationsTitle = "People Operations Specialist";
    private const string ProductDesignerTitle = "Product Designer";

    public static async Task SeedAsync(
        this AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStorage);
        ArgumentNullException.ThrowIfNull(timeProvider);

        await SeedFieldTechnicianAsync(dbContext, fileStorage, timeProvider, cancellationToken);
        await SeedWarehouseSupervisorAsync(dbContext, fileStorage, timeProvider, cancellationToken);
        await SeedPeopleOperationsAsync(dbContext, cancellationToken);
        await SeedProductDesignerAsync(dbContext, fileStorage, timeProvider, cancellationToken);
    }

    private static async Task SeedFieldTechnicianAsync(
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (await HasVacancyAsync(dbContext, FieldTechnicianTitle, cancellationToken))
        {
            return;
        }

        var vacancy = CreateVacancy(
            FieldTechnicianTitle,
            new DateOnly(2026, 8, 1),
            ["Electrical Systems", "PLC Programming", "Safety Certification"],
            2);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        var round = vacancy.Rounds.Single();
        var candidates = await ImportCandidatesAsync(
            dbContext,
            fileStorage,
            timeProvider,
            vacancy,
            round,
            [
                new(
                    "budi-santoso",
                    "Budi Santoso — Strong Match",
                    "budi.santoso@example.com",
                    "Budi Santoso",
                    "budi.santoso@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [1, 2, 3],
                    ["cv-a.pdf"],
                    "budi-santoso.eml"),
                new(
                    "siti-rahma",
                    "Siti Rahma — Weak Match",
                    "siti.rahma@example.com",
                    "Siti Rahma",
                    "siti.rahma@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [],
                    ["cv-a.pdf"]),
                new(
                    "andi-wijaya",
                    "Andi Wijaya — Flagged",
                    "andi.wijaya@example.com",
                    "Andi Wijaya",
                    "andi.wijaya@example.com",
                    CandidateReviewStatus.Flagged,
                    "Verify the PLC experience before scheduling a practical test.",
                    [1, 2],
                    ["cv-a.pdf"]),
                new(
                    "dewi-lestari",
                    "Dewi Lestari — Shortlisted",
                    "dewi.lestari@example.com",
                    "Dewi Lestari",
                    "dewi.lestari@example.com",
                    CandidateReviewStatus.Shortlisted,
                    null,
                    [1, 2],
                    ["cv-a.pdf"]),
                new(
                    "rudi-hartono",
                    "Rudi Hartono — Rejected",
                    "rudi.hartono@example.com",
                    "Rudi Hartono",
                    "rudi.hartono@example.com",
                    CandidateReviewStatus.Rejected,
                    null,
                    [1],
                    ["cv-a.pdf"]),
                new(
                    "maya-putri",
                    null,
                    null,
                    "Maya Putri — No Contact Details",
                    "maya.putri@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [],
                    []),
                new(
                    "joko-prasetyo",
                    "Joko Prasetyo — Multiple PDFs",
                    "joko.prasetyo@example.com",
                    "Joko Prasetyo",
                    "joko.prasetyo@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [1],
                    ["cv-a.pdf", "cv-b.pdf"],
                    "joko-prasetyo.eml")
            ],
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        ApplyCandidateStates(vacancy, round, candidates);
        SeedTemplate(
            vacancy,
            EmailTemplateKind.Shortlisted,
            "Next steps for {{candidate_name}}",
            "Hi {{candidate_name}},\n\nWe would like to continue your application for {{vacancy_title}}.");
        SeedTemplate(
            vacancy,
            EmailTemplateKind.Rejected,
            "Update on your {{vacancy_title}} application",
            "Hi {{candidate_name}},\n\nThank you for your interest in {{vacancy_title}}. We will not be moving forward at this time.");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedWarehouseSupervisorAsync(
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (await HasVacancyAsync(dbContext, WarehouseSupervisorTitle, cancellationToken))
        {
            return;
        }

        var vacancy = CreateVacancy(
            WarehouseSupervisorTitle,
            new DateOnly(2026, 8, 5),
            ["Inventory Management", "Forklift Operation", "Team Leadership"],
            2);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        var firstRound = vacancy.Rounds.Single();
        var firstRoundCandidates = await ImportCandidatesAsync(
            dbContext,
            fileStorage,
            timeProvider,
            vacancy,
            firstRound,
            [
                new(
                    "dian-sutanto",
                    "Dian Sutanto — New",
                    "dian.sutanto@example.com",
                    "Dian Sutanto",
                    "dian.sutanto@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [],
                    ["cv-a.pdf"]),
                new(
                    "ayu-kusuma",
                    "Ayu Kusuma — Flagged",
                    "ayu.kusuma@example.com",
                    "Ayu Kusuma",
                    "ayu.kusuma@example.com",
                    CandidateReviewStatus.Flagged,
                    "Confirm the team-size claim with a reference.",
                    [1],
                    ["cv-a.pdf"]),
                new(
                    "eko-pratama",
                    "Eko Pratama — Shortlisted",
                    "eko.pratama@example.com",
                    "Eko Pratama",
                    "eko.pratama@example.com",
                    CandidateReviewStatus.Shortlisted,
                    "Strong operations background; keep available for promotion.",
                    [1, 2],
                    ["cv-a.pdf"]),
                new(
                    "fitri-lestari",
                    "Fitri Lestari — Rejected",
                    "fitri.lestari@example.com",
                    "Fitri Lestari",
                    "fitri.lestari@example.com",
                    CandidateReviewStatus.Rejected,
                    null,
                    [],
                    ["cv-a.pdf"])
            ],
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        ApplyCandidateStates(vacancy, firstRound, firstRoundCandidates);
        await dbContext.SaveChangesAsync(cancellationToken);

        EnsureSuccess(vacancy.CloseRound(firstRound.Id, timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);

        var secondRound = Require(vacancy.CreateRound("Returning applicants"));
        await dbContext.SaveChangesAsync(cancellationToken);

        var secondRoundCandidates = await ImportCandidatesAsync(
            dbContext,
            fileStorage,
            timeProvider,
            vacancy,
            secondRound,
            [
                new(
                    "ayu-kusuma-returning",
                    "Ayu Kusuma — Returning Applicant",
                    "ayu.kusuma@example.com",
                    "Ayu Kusuma",
                    "ayu.kusuma@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [],
                    ["cv-a.pdf"]),
                new(
                    "gilang-saputra",
                    "Gilang Saputra — New",
                    "gilang.saputra@example.com",
                    "Gilang Saputra",
                    "gilang.saputra@example.com",
                    CandidateReviewStatus.New,
                    null,
                    [],
                    ["cv-a.pdf"])
            ],
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ApplyCandidateStates(vacancy, secondRound, secondRoundCandidates);
        SeedTemplate(
            vacancy,
            EmailTemplateKind.Shortlisted,
            "Warehouse Supervisor next steps for {{candidate_name}}",
            "Hi {{candidate_name}},\n\nWe would like to continue your application for {{vacancy_title}}.");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPeopleOperationsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (await HasVacancyAsync(dbContext, PeopleOperationsTitle, cancellationToken))
        {
            return;
        }

        var vacancy = CreateVacancy(
            PeopleOperationsTitle,
            new DateOnly(2026, 8, 10),
            ["Recruitment", "Employee Relations", "HRIS"],
            1);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        var round = vacancy.Rounds.Single();
        EnsureSuccess(vacancy.CloseRound(round.Id, new DateTimeOffset(2026, 8, 31, 17, 0, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductDesignerAsync(
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (await HasVacancyAsync(dbContext, ProductDesignerTitle, cancellationToken))
        {
            return;
        }

        var vacancy = CreateVacancy(
            ProductDesignerTitle,
            new DateOnly(2026, 7, 1),
            ["Figma", "User Research", "Design Systems"],
            1);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(cancellationToken);

        var round = vacancy.Rounds.Single();
        var candidates = await ImportCandidatesAsync(
            dbContext,
            fileStorage,
            timeProvider,
            vacancy,
            round,
            [
                new(
                    "sari-maharani",
                    "Sari Maharani — Read Only",
                    "sari.maharani@example.com",
                    "Sari Maharani",
                    "sari.maharani@example.com",
                    CandidateReviewStatus.Shortlisted,
                    "Closed-vacancy review fixture.",
                    [1, 2, 3],
                    ["cv-a.pdf"]),
                new(
                    "toni-wibowo",
                    "Toni Wibowo — Read Only",
                    "toni.wibowo@example.com",
                    "Toni Wibowo",
                    "toni.wibowo@example.com",
                    CandidateReviewStatus.Rejected,
                    "Closed-vacancy review fixture.",
                    [1],
                    ["cv-a.pdf"])
            ],
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        ApplyCandidateStates(vacancy, round, candidates);
        await dbContext.SaveChangesAsync(cancellationToken);

        EnsureSuccess(vacancy.CloseRound(round.Id, new DateTimeOffset(2026, 8, 14, 17, 0, 0, TimeSpan.Zero)));
        EnsureSuccess(vacancy.Close(new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SeededCandidate>> ImportCandidatesAsync(
        AppDbContext dbContext,
        IPrivateFileStorage fileStorage,
        TimeProvider timeProvider,
        Vacancy vacancy,
        IntakeRound round,
        IReadOnlyList<CandidateSeed> definitions,
        CancellationToken cancellationToken)
    {
        var importedAt = timeProvider.GetUtcNow();
        var candidates = new List<SeededCandidate>(definitions.Count);

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var sourceBytes = definition.SourceFixture is null
                ? Encoding.UTF8.GetBytes(CreateSyntheticEmail(vacancy.Title, definition))
                : await ReadFixtureAsync(definition.SourceFixture, cancellationToken);
            var storedSource = await fileStorage.StoreAsync(
                sourceBytes,
                "source-emails",
                ".eml",
                cancellationToken);
            var documents = new List<StoredCvDocument>(definition.CvFixtures.Count);

            for (var documentIndex = 0; documentIndex < definition.CvFixtures.Count; documentIndex++)
            {
                var fixtureName = definition.CvFixtures[documentIndex];
                var documentBytes = await ReadFixtureAsync(fixtureName, cancellationToken);
                var storedDocument = await fileStorage.StoreAsync(
                    documentBytes,
                    "cv-documents",
                    ".pdf",
                    cancellationToken);
                documents.Add(new StoredCvDocument(
                    fixtureName,
                    storedDocument.StorageKey,
                    documentIndex + 1,
                    definition.CvFixtures.Count == 1,
                    storedDocument.SizeBytes,
                    storedDocument.Sha256));
            }

            var candidate = Require(vacancy.ImportCandidate(new CandidateImportData(
                round.Id,
                definition.SenderName,
                definition.SenderEmail,
                $"Application for {vacancy.Title}",
                $"Seed scenario: {definition.Key}.",
                SourceSentAt(index),
                definition.SourceFixture ?? $"{definition.Key}.eml",
                storedSource.StorageKey,
                storedSource.SizeBytes,
                storedSource.Sha256,
                importedAt,
                documents)));
            dbContext.Candidates.Add(candidate);
            candidates.Add(new SeededCandidate(definition, candidate));
        }

        return candidates;
    }

    private static void ApplyCandidateStates(
        Vacancy vacancy,
        IntakeRound round,
        IReadOnlyList<SeededCandidate> seededCandidates)
    {
        foreach (var seededCandidate in seededCandidates)
        {
            var definition = seededCandidate.Definition;
            var candidateId = seededCandidate.Candidate.Id;

            if (definition.FullName is not null || definition.ContactEmail is not null)
            {
                EnsureSuccess(vacancy.UpdateCandidateDetails(
                    round.Id,
                    candidateId,
                    definition.FullName,
                    definition.ContactEmail));
            }

            if (definition.ReviewStatus != CandidateReviewStatus.New || definition.Notes is not null)
            {
                EnsureSuccess(vacancy.ReviewCandidate(
                    round.Id,
                    candidateId,
                    definition.ReviewStatus,
                    definition.Notes));
            }

            foreach (var position in definition.ConfirmedRequirementPositions)
            {
                var requirement = vacancy.Requirements[position - 1];
                EnsureSuccess(vacancy.ReviewCandidateRequirement(
                    round.Id,
                    candidateId,
                    requirement.Id,
                    true));
            }

            if (definition.HireOutcome != CandidateHireOutcome.None)
            {
                EnsureSuccess(vacancy.SetCandidateHireOutcome(
                    round.Id,
                    candidateId,
                    definition.HireOutcome,
                    null));
            }
        }
    }

    private static void SeedTemplate(
        Vacancy vacancy,
        EmailTemplateKind kind,
        string subject,
        string body) => EnsureSuccess(vacancy.UpsertEmailTemplate(kind, subject, body));

    private static Vacancy CreateVacancy(
        string title,
        DateOnly openedOn,
        IReadOnlyList<string> requirements,
        int? neededHires) => Require(Vacancy.Create(title, openedOn, requirements, neededHires));

    private static async Task<bool> HasVacancyAsync(
        AppDbContext dbContext,
        string title,
        CancellationToken cancellationToken) =>
        await dbContext.Vacancies.AnyAsync(vacancy => vacancy.Title == title, cancellationToken);

    private static async Task<byte[]> ReadFixtureAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Infrastructure",
            "Seed",
            "Fixtures",
            fileName);
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    private static string CreateSyntheticEmail(string vacancyTitle, CandidateSeed definition) =>
        $"From: {definition.SenderName} <{definition.SenderEmail}>\r\n" +
        $"Subject: Application for {vacancyTitle}\r\n\r\n" +
        $"Seed scenario: {definition.Key}.\r\n";

    private static DateTimeOffset SourceSentAt(int index) =>
        new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero).AddMinutes(index);

    private static T Require<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return result.Value;
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }
    }

    private sealed record CandidateSeed(
        string Key,
        string? FullName,
        string? ContactEmail,
        string SenderName,
        string SenderEmail,
        CandidateReviewStatus ReviewStatus,
        string? Notes,
        IReadOnlyList<int> ConfirmedRequirementPositions,
        IReadOnlyList<string> CvFixtures,
        string? SourceFixture = null,
        CandidateHireOutcome HireOutcome = CandidateHireOutcome.None);

    private sealed record SeededCandidate(CandidateSeed Definition, Candidate Candidate);
}