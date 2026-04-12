using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiroBot.MilkyAdapter.Milky;

internal static class MilkyJson
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new SnakeCaseEnumJsonConverterFactory());
        options.Converters.Add(new EventJsonConverter());
        options.Converters.Add(new IncomingSegmentJsonConverter());
        options.Converters.Add(new OutgoingSegmentJsonConverter());
        return options;
    }
}

internal sealed class OutgoingSegmentJsonConverter : JsonConverter<OutgoingSegment>
{
    public override OutgoingSegment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("OutgoingSegment 只用于发送，不支持反序列化。");

    public override void Write(Utf8JsonWriter writer, OutgoingSegment value, JsonSerializerOptions options)
    {
        var segmentType = value.GetType();
        var typeName = segmentType.Name;
        if (typeName.EndsWith("OutgoingSegment", StringComparison.Ordinal))
        {
            typeName = typeName[..^"OutgoingSegment".Length];
        }

        writer.WriteStartObject();
        writer.WriteString("type", ToSnakeCase(typeName));
        writer.WritePropertyName("data");
        JsonSerializer.Serialize(writer, (object)value, segmentType, options);
        writer.WriteEndObject();
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current) && i > 0)
            {
                var previous = value[i - 1];
                var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || nextIsLower)
                {
                    builder.Append('_');
                }
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}
