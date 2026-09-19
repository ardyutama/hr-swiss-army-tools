using System.Text.Json;
using System.Text.Json.Serialization;
using hr_sat.Domain.Vacancies;

namespace hr_sat.Web.Api;

internal sealed class ScreeningOperatorJsonConverter : JsonConverter<ScreeningOperator>
{
    public override ScreeningOperator Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A Screening Rule operator must be a string.");
        }

        return reader.GetString()?.Trim().ToLowerInvariant() switch
        {
            "equals" => ScreeningOperator.Equals,
            "not-equals" or "notequals" => ScreeningOperator.NotEquals,
            "is-empty" or "isempty" => ScreeningOperator.IsEmpty,
            "not-empty" or "notempty" => ScreeningOperator.NotEmpty,
            "contains" => ScreeningOperator.Contains,
            _ => throw new JsonException("The Screening Rule operator is invalid.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ScreeningOperator value,
        JsonSerializerOptions options)
    {
        var name = value switch
        {
            ScreeningOperator.Equals => "equals",
            ScreeningOperator.NotEquals => "not-equals",
            ScreeningOperator.IsEmpty => "is-empty",
            ScreeningOperator.NotEmpty => "not-empty",
            ScreeningOperator.Contains => "contains",
            _ => throw new JsonException("The Screening Rule operator is invalid.")
        };
        writer.WriteStringValue(name);
    }
}