using hr_sat.Application.Features.EmailTemplates.Delete;
using hr_sat.Application.Features.EmailTemplates.Render;
using hr_sat.Application.Features.EmailTemplates.Upsert;
using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.EmailTemplates;

public sealed class EmailTemplateHandlerTests
{
    [Fact]
    public async Task Handle_Should_CreateAndReplaceTemplate_WhenUpsertedTwice()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertEmailTemplateCommandHandler(dbContext);

        var createResult = await handler.Handle(
            new UpsertEmailTemplateCommand(
                vacancy.Id,
                "shortlisted",
                "  Welcome  ",
                "Hello {{candidate_name}}."),
            CancellationToken.None);

        createResult.IsSuccess.ShouldBeTrue();
        var templateId = createResult.Value.Id;

        var replaceResult = await handler.Handle(
            new UpsertEmailTemplateCommand(
                vacancy.Id,
                "SHORTLISTED",
                "Updated subject",
                "Updated body."),
            CancellationToken.None);

        replaceResult.IsSuccess.ShouldBeTrue();
        replaceResult.Value.Id.ShouldBe(templateId);
        replaceResult.Value.Subject.ShouldBe("Updated subject");
        replaceResult.Value.Body.ShouldBe("Updated body.");
        var persisted = await dbContext.EmailTemplates.SingleAsync();
        persisted.Subject.ShouldBe("Updated subject");
        persisted.Body.ShouldBe("Updated body.");
        vacancy.DomainEvents.ShouldContain(item => item is EmailTemplateUpsertedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_DeleteTemplate_WhenItExists()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var upsertHandler = new UpsertEmailTemplateCommandHandler(dbContext);
        await upsertHandler.Handle(
            new UpsertEmailTemplateCommand(
                vacancy.Id,
                "rejected",
                "Not this time",
                "Thank you for applying."),
            CancellationToken.None);
        var deleteHandler = new DeleteEmailTemplateCommandHandler(dbContext);

        var result = await deleteHandler.Handle(
            new DeleteEmailTemplateCommand(vacancy.Id, "rejected"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.EmailTemplates.CountAsync()).ShouldBe(0);
        vacancy.DomainEvents.ShouldContain(item => item is EmailTemplateDeletedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenDeletingMissingTemplate()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy();
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new DeleteEmailTemplateCommandHandler(dbContext);

        var result = await handler.Handle(
            new DeleteEmailTemplateCommand(vacancy.Id, "rejected"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EmailTemplateErrors.NotFound(vacancy.Id, EmailTemplateKind.Rejected));
    }

    [Fact]
    public void DeleteEmailTemplate_Should_ReturnValidationError_WhenKindIsUnsupported()
    {
        var vacancy = CandidateTestData.CreateVacancy();

        var result = vacancy.DeleteEmailTemplate((EmailTemplateKind)999);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        result.Error.Code.ShouldBe("EmailTemplates.Invalid");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenUpsertingIntoClosedVacancy()
    {
        await using var dbContext = new TestDbContext();
        var vacancy = CandidateTestData.CreateVacancy(closed: true);
        dbContext.Vacancies.Add(vacancy);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var handler = new UpsertEmailTemplateCommandHandler(dbContext);

        var result = await handler.Handle(
            new UpsertEmailTemplateCommand(
                vacancy.Id,
                "shortlisted",
                "Subject",
                "Body"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(EmailTemplateErrors.VacancyClosed(vacancy.Id));
        (await dbContext.EmailTemplates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_RenderKnownTokensAndPreserveUnknownTokens_WhenCandidateBelongsToVacancy()
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var handler = new RenderEmailTemplateQueryHandler(dbContext);

        var result = await handler.Handle(
            new RenderEmailTemplateQuery(
                vacancy.Id,
                candidate.Id,
                "Hello {{ Candidate_Name }}",
                "{{candidate_name}} - {{ vacancy_title }} - {{unknown_token}}"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Subject.ShouldBe("Hello Candidate 1");
        result.Value.Body.ShouldBe("Candidate 1 - Data Analyst - {{unknown_token}}");
    }
}