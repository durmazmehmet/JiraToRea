using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraToRea.App.Serialization;

public sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetRawText();
            case JsonTokenType.Null:
            case JsonTokenType.Undefined:
                return null;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.GetRawText();
                }
            default:
                throw new JsonException($"Unsupported token type '{reader.TokenType}' for flexible string conversion.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
