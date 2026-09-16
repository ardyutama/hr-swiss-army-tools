namespace hr_sat.Web.Api.Endpoints.FormLayouts;

internal sealed class UpsertRequest
{
    public IReadOnlyList<string>? HeaderSnapshot { get; init; }
    public int? NameColumnOrdinal { get; init; }
    public int? ContactEmailColumnOrdinal { get; init; }
    public int? ContactPhoneColumnOrdinal { get; init; }
    public int? CvLinkColumnOrdinal { get; init; }
}