using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraToRea.App.Serialization;

public sealed class DateOnlyJsonConverter : JsonConverter<DateTime>
{
    private const string DateFormat = "yyyy-MM-dd";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (!string.IsNullOrWhiteSpace(value) &&
                DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }

            if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            {
                return fallback.Date;
            }
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var ticks))
        {
            return new DateTime(ticks);
        }

        var rawValue = reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? string.Empty
            : reader.GetRawText();
        throw new JsonException($"Unable to convert token '{rawValue}' to a date using format '{DateFormat}'.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(DateFormat, CultureInfo.InvariantCulture));
    }
}
