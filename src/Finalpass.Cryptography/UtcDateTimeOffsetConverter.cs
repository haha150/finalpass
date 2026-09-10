using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Finalpass.Cryptography;

internal sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        string? value = reader.GetString();
        if (!DateTimeOffset.TryParseExact(
            value,
            Format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset result))
        {
            throw new JsonException("A timestamp is invalid or is not canonical UTC.");
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Timestamps must be UTC.");
        }

        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
