using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShiroBot.Model.Common;
using ShiroBot.SDK.Abstractions;

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
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new SnakeCaseEnumJsonConverterFactory());
        options.Converters.Add(new EventJsonConverter());
        options.Converters.Add(new IncomingMessageJsonConverter());
        options.Converters.Add(new IncomingSegmentJsonConverter());
        options.Converters.Add(new GroupNotificationJsonConverter());
        options.Converters.Add(new GetGroupNotificationsResponseJsonConverter());
        options.Converters.Add(new OutgoingSegmentJsonConverter());
        return options;
    }
}

internal sealed class IncomingMessageJsonConverter : JsonConverter<IncomingMessage>
{
    private static readonly Dictionary<string, Type> MessageSceneTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["group"] = typeof(GroupIncomingMessage),
        ["friend"] = typeof(FriendIncomingMessage),
        ["temp"] = typeof(TempIncomingMessage)
    };

    public override IncomingMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var payload = document.RootElement;

        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Incoming message payload must be a JSON object.");
        }

        if (!payload.TryGetProperty("message_scene", out var sceneElement) ||
            sceneElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Incoming message payload missing message_scene.");
        }

        var scene = sceneElement.GetString();
        if (scene is null || !MessageSceneTypes.TryGetValue(scene, out var targetType))
        {
            throw new JsonException($"Unsupported message_scene '{scene}'.");
        }

        var result = payload.Deserialize(targetType, options) as IncomingMessage;
        return result ?? throw new JsonException($"Cannot deserialize incoming message as {targetType.Name}.");
    }

    public override void Write(Utf8JsonWriter writer, IncomingMessage value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

internal sealed class GroupNotificationJsonConverter : JsonConverter<GroupNotification>
{
    private static readonly Dictionary<string, Type> NotificationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["join_request"] = typeof(JoinRequestGroupNotification),
        ["admin_change"] = typeof(AdminChangeGroupNotification),
        ["kick"] = typeof(KickGroupNotification),
        ["quit"] = typeof(QuitGroupNotification),
        ["invited_join_request"] = typeof(InvitedJoinRequestGroupNotification)
    };

    public override GroupNotification Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var payload = document.RootElement;

        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Milky group notification payload must be a JSON object.");
        }

        if (!payload.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return new UnknownMilkyGroupNotification("<missing>", payload.Clone());
        }

        var notificationType = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(notificationType) ||
            !NotificationTypes.TryGetValue(notificationType, out var targetType))
        {
            return new UnknownMilkyGroupNotification(notificationType ?? "<empty>", payload.Clone());
        }

        return payload.Deserialize(targetType, options) as GroupNotification
               ?? throw new JsonException(
                   $"Cannot deserialize Milky group notification '{notificationType}' as {targetType.Name}.");
    }

    public override void Write(Utf8JsonWriter writer, GroupNotification value, JsonSerializerOptions options)
    {
        var notificationType = value switch
        {
            JoinRequestGroupNotification => "join_request",
            AdminChangeGroupNotification => "admin_change",
            KickGroupNotification => "kick",
            QuitGroupNotification => "quit",
            InvitedJoinRequestGroupNotification => "invited_join_request",
            _ => throw new JsonException($"Unsupported Milky group notification type {value.GetType().FullName}.")
        };

        var payload = JsonSerializer.SerializeToElement(value, value.GetType(), options);
        writer.WriteStartObject();
        writer.WriteString("type", notificationType);
        foreach (var property in payload.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

internal sealed class GetGroupNotificationsResponseJsonConverter : JsonConverter<GetGroupNotificationsResponse>
{
    public override GetGroupNotificationsResponse Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var payload = document.RootElement;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("notifications", out var notificationsElement) ||
            notificationsElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Milky group notifications response missing notifications array.");
        }

        var notifications = new List<GroupNotification>();
        foreach (var item in notificationsElement.EnumerateArray())
        {
            try
            {
                var notification = item.Deserialize<GroupNotification>(options);
                if (notification is UnknownMilkyGroupNotification unknown)
                {
                    BotLog.Warning($"忽略未知 Milky 群通知类型 '{unknown.NotificationType}'。");
                    continue;
                }

                if (notification is not null)
                {
                    notifications.Add(notification);
                }
            }
            catch (JsonException ex)
            {
                BotLog.Warning($"忽略无法解析的 Milky 群通知: {ex.Message}");
            }
        }

        long? nextNotificationSeq = null;
        if (payload.TryGetProperty("next_notification_seq", out var nextElement) &&
            nextElement.ValueKind != JsonValueKind.Null)
        {
            if (nextElement.ValueKind != JsonValueKind.Number || !nextElement.TryGetInt64(out var nextValue))
            {
                throw new JsonException("Milky group notifications response has invalid next_notification_seq.");
            }

            nextNotificationSeq = nextValue;
        }

        return new GetGroupNotificationsResponse(notifications, nextNotificationSeq);
    }

    public override void Write(
        Utf8JsonWriter writer,
        GetGroupNotificationsResponse value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("notifications");
        JsonSerializer.Serialize(writer, value.Notifications, options);
        if (value.NextNotificationSeq is { } nextNotificationSeq)
        {
            writer.WriteNumber("next_notification_seq", nextNotificationSeq);
        }

        writer.WriteEndObject();
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

internal sealed record UnknownMilkyGroupNotification(string NotificationType, JsonElement Payload)
    : GroupNotification;
