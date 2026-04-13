using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiroBot.MilkyAdapter.Milky;

public class MilkyClient(HttpClient httpClient)
{
    public MilkyEventHandler Events { get; } = new(httpClient);

    public event Func<Event, Task>? EventReceived
    {
        add => Events.EventReceived += value;
        remove => Events.EventReceived -= value;
    }

    public async Task<TResponse> RequestAsync<TRequest, TResponse>(TRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var body = JsonContent.Create(request, options: JsonOptions);
        using var response = await httpClient.PostAsync($"api/{ToApiName(typeof(TRequest).Name)}", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return DeserializeResponse<TResponse>(json);
    }

    public async Task RequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
    {
        await RequestAsync<TRequest, JsonElement>(request, cancellationToken);
    }

    private static string ToApiName(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        var normalizedName = typeName.Trim();
        foreach (var suffix in new[] { "Request", "Response", "Async" })
        {
            if (normalizedName.EndsWith(suffix, StringComparison.Ordinal))
            {
                normalizedName = normalizedName[..^suffix.Length];
            }
        }

        return ToSnakeCase(normalizedName);
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

    private static TResponse DeserializeResponse<TResponse>(JsonElement json)
    {
        if (typeof(TResponse) == typeof(JsonElement) || typeof(TResponse) == typeof(JsonElement?))
        {
            return (TResponse)(object)json;
        }

        var wrapped = json.Deserialize<MilkyResult>(JsonOptions);
        if (wrapped is not null && wrapped.HasPayload)
        {
            return wrapped.GetResult<TResponse>(JsonOptions);
        }

        var direct = json.Deserialize<TResponse>(JsonOptions);
        return direct ?? throw new JsonException($"Failed to deserialize response to type {typeof(TResponse).FullName}.");
    }
}

internal sealed class SnakeCaseEnumJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var targetType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return targetType.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var targetType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        var converterType = typeof(SnakeCaseEnumJsonConverter<>).MakeGenericType(targetType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

internal sealed class SnakeCaseEnumJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intValue))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), intValue);
            }

            if (reader.TryGetInt64(out var longValue))
            {
                return (TEnum)Enum.ToObject(typeof(TEnum), longValue);
            }
        }

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Cannot convert JSON value to enum {typeof(TEnum).FullName}.");
        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        if (Enum.TryParse<TEnum>(raw, true, out var direct))
        {
            return direct;
        }

        var normalized = NormalizeEnumToken(raw);
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(NormalizeEnumToken(name), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<TEnum>(name, true);
            }
        }

        throw new JsonException($"Cannot convert JSON value to enum {typeof(TEnum).FullName}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToSnakeCase(value.ToString()));
    }

    private static string NormalizeEnumToken(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Where(char.IsLetterOrDigit))
        {
            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
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

internal sealed class MilkyResult
{
    [JsonPropertyName("data")]
    public JsonElement Data { get; init; }

    [JsonPropertyName("retcode")]
    public int? RetCode { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    public bool HasPayload => Data.ValueKind is not JsonValueKind.Undefined && Data.ValueKind is not JsonValueKind.Null;

    public T GetResult<T>(JsonSerializerOptions options)
    {
        if (RetCode is > 0)
        {
            throw new HttpRequestException($"Milky API business error: retcode={RetCode}, message={Message ?? "unknown"}");
        }

        if (typeof(T) == typeof(JsonElement) || typeof(T) == typeof(JsonElement?))
        {
            return (T)(object)Data;
        }

        var result = Data.Deserialize<T>(options);
        return result ?? throw new JsonException($"Cannot deserialize wrapped data as {typeof(T).FullName}.");
    }
}
