using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.FormLayouts;
using hr_sat.Application.Features.FormLayouts.Upsert;

namespace hr_sat.Web.Api.Endpoints.FormLayouts;

internal sealed class Upsert : IEndpoint
{
    public sealed class Request
    {
        public IReadOnlyList<string>? HeaderSnapshot { get; init; }
        public int? NameColumnOrdinal { get; init; }
        public int? ContactEmailColumnOrdinal { get; init; }
        public int? ContactPhoneColumnOrdinal { get; init; }
        public int? CvLinkColumnOrdinal { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut(
                "/api/vacancies/{vacancyId:long}/form-layout",
                async (
                    long vacancyId,
                    Request request,
                    ICommandHandler<UpsertFormLayoutCommand, FormLayoutResponse> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler.Handle(
                        new UpsertFormLayoutCommand(
                            vacancyId,
                            request.HeaderSnapshot,
                            request.NameColumnOrdinal,
                            request.ContactEmailColumnOrdinal,
                            request.ContactPhoneColumnOrdinal,
                            request.CvLinkColumnOrdinal),
                        cancellationToken);
                    return result.Match<IResult>(TypedResults.Ok, CustomResults.Problem);
                })
            .WithTags(Tags.FormLayouts)
            .WithName("UpsertFormLayout");
    }
}