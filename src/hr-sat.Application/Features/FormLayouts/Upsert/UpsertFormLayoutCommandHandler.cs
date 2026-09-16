using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Application.Features.FormLayouts.Upsert;

internal sealed class UpsertFormLayoutCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<UpsertFormLayoutCommand, FormLayoutResponse>
{
    public async Task<Result<FormLayoutResponse>> Handle(
        UpsertFormLayoutCommand command,
        CancellationToken cancellationToken)
    {
        var upsertResult = await VacancyWrite.ExecuteAsync(
            command.VacancyId,
            dbContext,
            vacancy =>
            {
                var hadLayout = vacancy.FormLayout is not null;
                var result = vacancy.UpsertFormLayout(new FormLayoutDefinition(
                    command.HeaderSnapshot,
                    command.NameColumnOrdinal,
                    command.ContactEmailColumnOrdinal,
                    command.ContactPhoneColumnOrdinal,
                    command.CvLinkColumnOrdinal));
                if (result.IsSuccess && !hadLayout)
                {
                    dbContext.FormLayouts.Add(result.Value);
                }

                return result;
            },
            cancellationToken);
        if (upsertResult.IsFailure)
        {
            return Result<FormLayoutResponse>.Failure(upsertResult.Error);
        }

        return FormLayoutResponse.From(upsertResult.Value.FormLayout!);
    }
}