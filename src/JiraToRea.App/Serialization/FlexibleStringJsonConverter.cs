using System;
using System.Globalization;
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
                if (reader.TryGetInt64(out var integerValue))
                {
                    return integerValue.ToString(CultureInfo.InvariantCulture);
                }

                if (reader.TryGetDouble(out var doubleValue))
                {
                    return doubleValue.ToString(CultureInfo.InvariantCulture);
                }

                return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            case JsonTokenType.True:
                return bool.TrueString;
            case JsonTokenType.False:
                return bool.FalseString;
            case JsonTokenType.Null:
            case JsonTokenType.None:
                return null;
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                using (var document = JsonDocument.ParseValue(ref reader))
                {
                    return document.RootElement.ToString();
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
