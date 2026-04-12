using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShiroBot.MilkyAdapter.Milky;

public sealed class MilkyEventHandler(HttpClient httpClient)
{
    private const string EventPath = "event";

    public event Func<Event, Task>? EventReceived;

    public async Task ReceivingEventUsingWebSocketAsync(CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("请先设置 HttpClient.BaseAddress");
        }

        using var socket = new ClientWebSocket();
        CopyDefaultHeaders(socket.Options, httpClient);

        await socket.ConnectAsync(BuildEventUri(httpClient.BaseAddress), cancellationToken);

        await using var bufferStream = new MemoryStream();
        var buffer = GC.AllocateUninitializedArray<byte>(4096);

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.Count > 0)
            {
                await bufferStream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            if (bufferStream.Length > 0)
            {
                bufferStream.Position = 0;
                using var document = await JsonDocument.ParseAsync(bufferStream, cancellationToken: cancellationToken);
                await PublishIfNotNullAsync(document.RootElement.Deserialize<Event>(JsonOptions));
            }

            bufferStream.SetLength(0);
            bufferStream.Position = 0;
        }
    }

    public async Task ReceivingEventUsingSseAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EventPath);
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var payload = new StringBuilder();
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                await FlushSsePayloadAsync(payload);
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                payload.AppendLine(line[5..].TrimStart());
            }
        }

        await FlushSsePayloadAsync(payload);
    }

    public async Task ReceivingEventUsingWebhookAsync(string webhookUrl, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("WebhookUrl 不能为空。", nameof(webhookUrl));
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add(NormalizeWebhookPrefix(webhookUrl));
        listener.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await HandleWebhookAsync(context, token, cancellationToken);
        }
    }

    private async Task HandleWebhookAsync(HttpListenerContext context, string? token, CancellationToken cancellationToken)
    {
        try
        {
            if (context.Request.HttpMethod != HttpMethod.Post.Method)
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.MethodNotAllowed, "Method Not Allowed", cancellationToken);
                return;
            }

            if (!IsWebhookAuthorized(context.Request, token))
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.Unauthorized, "Unauthorized", cancellationToken);
                return;
            }

            using var document = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: cancellationToken);
            var data = document.RootElement.Deserialize<Event>(JsonOptions);
            if (data is null)
            {
                await WriteResponseAsync(context.Response, HttpStatusCode.BadRequest, "Unknown Event", cancellationToken);
                return;
            }

            await PublishAsync(data);
            await WriteResponseAsync(context.Response, HttpStatusCode.OK, "OK", cancellationToken);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task FlushSsePayloadAsync(StringBuilder payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        var data = Deserialize(payload.ToString());
        payload.Clear();
        await PublishIfNotNullAsync(data);
    }

    private Event? Deserialize(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Deserialize<Event>(JsonOptions);
    }

    private async Task PublishIfNotNullAsync(Event? data)
    {
        if (data is not null)
        {
            await PublishAsync(data);
        }
    }

    private async Task PublishAsync(Event data)
    {
        var handler = EventReceived;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<Func<Event, Task>>())
        {
            await subscriber(data);
        }
    }
    

    private static Uri BuildEventUri(Uri baseAddress) =>
        new(new UriBuilder(baseAddress)
        {
            Scheme = baseAddress.Scheme switch
            {
                "https" => "wss",
                "http" => "ws",
                _ => baseAddress.Scheme
            },
            Path = "/" + EventPath,
            Query = string.Empty
        }.Uri.AbsoluteUri);

    private static void CopyDefaultHeaders(ClientWebSocketOptions options, HttpClient client)
    {
        foreach (var header in client.DefaultRequestHeaders)
        {
            if (header.Key is "Accept" or "Connection" or "Upgrade" or "Host")
            {
                continue;
            }

            var values = header.Value.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (values.Length > 0)
            {
                options.SetRequestHeader(header.Key, string.Join(", ", values));
            }
        }
    }

    private static bool IsWebhookAuthorized(HttpListenerRequest request, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        var authHeader = request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(authHeader["Bearer ".Length..], token, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(request.Headers["X-Webhook-Token"], token, StringComparison.Ordinal) ||
               string.Equals(request.QueryString["token"], token, StringComparison.Ordinal);
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, HttpStatusCode statusCode, string content, CancellationToken cancellationToken)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "text/plain; charset=utf-8";

        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, cancellationToken);
    }

    private static string NormalizeWebhookPrefix(string webhookUrl) =>
        webhookUrl.Trim().EndsWith('/') ? webhookUrl.Trim() : webhookUrl.Trim() + "/";
}

internal sealed class IncomingSegmentJsonConverter : JsonConverter<IncomingSegment>
{
    private static readonly Type[] DirectSegmentTypes =
    [
        typeof(TextIncomingSegment),
        typeof(MentionIncomingSegment),
        typeof(MentionAllIncomingSegment),
        typeof(ReplyIncomingSegment),
        typeof(FaceIncomingSegment),
        typeof(MarketFaceIncomingSegment),
        typeof(ImageIncomingSegment),
        typeof(VideoIncomingSegment),
        typeof(RecordIncomingSegment),
        typeof(FileIncomingSegment),
        typeof(ForwardIncomingSegment),
        typeof(LightAppIncomingSegment),
        typeof(XmlIncomingSegment)
    ];

    private static readonly Dictionary<string, Type> WrappedSegmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = typeof(TextIncomingSegment),
        ["mention"] = typeof(MentionIncomingSegment),
        ["at"] = typeof(MentionIncomingSegment),
        ["mention_all"] = typeof(MentionAllIncomingSegment),
        ["at_all"] = typeof(MentionAllIncomingSegment),
        ["reply"] = typeof(ReplyIncomingSegment),
        ["face"] = typeof(FaceIncomingSegment),
        ["market_face"] = typeof(MarketFaceIncomingSegment),
        ["image"] = typeof(ImageIncomingSegment),
        ["video"] = typeof(VideoIncomingSegment),
        ["record"] = typeof(RecordIncomingSegment),
        ["audio"] = typeof(RecordIncomingSegment),
        ["file"] = typeof(FileIncomingSegment),
        ["forward"] = typeof(ForwardIncomingSegment),
        ["light_app"] = typeof(LightAppIncomingSegment),
        ["xml"] = typeof(XmlIncomingSegment)
    };

    public override IncomingSegment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        if (TryReadWrappedSegment(element, options) is { } wrapped)
        {
            return wrapped;
        }

        foreach (var segmentType in DirectSegmentTypes)
        {
            if (TryDeserialize(element, segmentType, options) is IncomingSegment segment)
            {
                return segment;
            }
        }

        throw new JsonException("Cannot determine incoming segment type.");
    }

    public override void Write(Utf8JsonWriter writer, IncomingSegment value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static IncomingSegment? TryReadWrappedSegment(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("data", out var dataElement) ||
            dataElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = typeElement.GetString();
        return type is not null && WrappedSegmentTypes.TryGetValue(type, out var segmentType)
            ? TryDeserialize(dataElement, segmentType, options) as IncomingSegment
            : null;
    }

    private static object? TryDeserialize(JsonElement element, Type targetType, JsonSerializerOptions options)
    {
        try
        {
            return element.Deserialize(targetType, options);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class EventJsonConverter : JsonConverter<Event>
{
    private static readonly Dictionary<string, Type> EventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["message_recall"] = typeof(MessageRecallEvent),
        ["friend_request"] = typeof(FriendRequestEvent),
        ["group_join_request"] = typeof(GroupJoinRequestEvent),
        ["group_invited_join_request"] = typeof(GroupInvitedJoinRequestEvent),
        ["group_invitation"] = typeof(GroupInvitationEvent),
        ["friend_nudge"] = typeof(FriendNudgeEvent),
        ["friend_file_upload"] = typeof(FriendFileUploadEvent),
        ["group_admin_change"] = typeof(GroupAdminChangeEvent),
        ["group_essence_message_change"] = typeof(GroupEssenceMessageChangeEvent),
        ["group_member_increase"] = typeof(GroupMemberIncreaseEvent),
        ["group_member_decrease"] = typeof(GroupMemberDecreaseEvent),
        ["group_name_change"] = typeof(GroupNameChangeEvent),
        ["group_message_reaction"] = typeof(GroupMessageReactionEvent),
        ["group_mute"] = typeof(GroupMuteEvent),
        ["group_whole_mute"] = typeof(GroupWholeMuteEvent),
        ["group_nudge"] = typeof(GroupNudgeEvent),
        ["group_file_upload"] = typeof(GroupFileUploadEvent),
        ["bot_offline"] = typeof(BotOfflineEvent),
        ["peer_pin_change"] = typeof(PeerPinChangeEvent)
    };

    private static readonly Dictionary<string, Type> MessageSceneTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["group"] = typeof(GroupIncomingMessage),
        ["friend"] = typeof(FriendIncomingMessage),
        ["temp"] = typeof(TempIncomingMessage)
    };

    public override Event Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Milky event payload must be a JSON object.");
        }

        var payload = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object
            ? dataElement
            : root;

        var eventType = root.TryGetProperty("event_type", out var eventTypeElement) && eventTypeElement.ValueKind == JsonValueKind.String
            ? eventTypeElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new JsonException("Milky event payload missing event_type.");
        }

        if (string.Equals(eventType, "message_receive", StringComparison.OrdinalIgnoreCase))
        {
            return DeserializeMessageReceive(payload, options);
        }

        if (EventTypes.TryGetValue(eventType, out var targetType))
        {
            return DeserializeRequired(payload, targetType, options, eventType);
        }

        throw new JsonException($"Unsupported Milky event_type '{eventType}'.");
    }

    public override void Write(Utf8JsonWriter writer, Event value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static Event DeserializeMessageReceive(JsonElement payload, JsonSerializerOptions options)
    {
        if (!payload.TryGetProperty("message_scene", out var sceneElement) ||
            sceneElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("message_receive event missing message_scene.");
        }

        var scene = sceneElement.GetString();
        if (scene is not null && MessageSceneTypes.TryGetValue(scene, out var targetType))
        {
            return DeserializeRequired(payload, targetType, options, $"message_receive/{scene}");
        }

        throw new JsonException($"Unsupported message_scene '{scene}'.");
    }

    private static Event DeserializeRequired(JsonElement payload, Type targetType, JsonSerializerOptions options, string eventType)
    {
        var result = payload.Deserialize(targetType, options) as Event;
        return result ?? throw new JsonException($"Cannot deserialize Milky event '{eventType}' as {targetType.Name}.");
    }
}
