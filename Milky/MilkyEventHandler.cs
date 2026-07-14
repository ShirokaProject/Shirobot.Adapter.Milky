using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShiroBot.SDK.Abstractions;

namespace ShiroBot.MilkyAdapter.Milky;

public sealed class MilkyEventHandler(HttpClient httpClient)
{
    public event Func<Event, Task>? EventReceived;

    public async Task ReceivingEventUsingWebSocketAsync(CancellationToken cancellationToken = default)
    {
        var baseAddress = httpClient.BaseAddress ?? throw new InvalidOperationException("请先设置 HttpClient.BaseAddress");
        var wsAddress = new Uri(new UriBuilder(baseAddress)
        {
            Scheme = baseAddress.Scheme switch
            {
                "https" => "wss",
                "http" => "ws",
                _ => baseAddress.Scheme
            },
            Path = $"{baseAddress.AbsolutePath.TrimEnd('/')}/event"
        }.Uri.AbsoluteUri);
        
        using var socket = new ClientWebSocket();
        foreach (var header in httpClient.DefaultRequestHeaders)
        {
            if (header.Key is "Accept" or "Connection" or "Upgrade" or "Host")
            {
                continue;
            }

            var values = header.Value.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
            if (values.Length > 0)
            {
                socket.Options.SetRequestHeader(header.Key, string.Join(", ", values));
            }
        }
        await socket.ConnectAsync(wsAddress, cancellationToken);

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
                var payload = Encoding.UTF8.GetString(bufferStream.ToArray());
                await PublishIfNotNullAsync(DeserializeEvent(payload, "WebSocket"));
            }

            bufferStream.SetLength(0);
            bufferStream.Position = 0;
        }
    }

    public async Task ReceivingEventUsingSseAsync(CancellationToken cancellationToken)
    {
        var baseAddress = httpClient.BaseAddress ?? throw new InvalidOperationException("请先设置 HttpClient.BaseAddress");
        var eventUri = new UriBuilder(baseAddress)
        {
            Path = $"{baseAddress.AbsolutePath.TrimEnd('/')}/event"
        }.Uri;

        using var request = new HttpRequestMessage(HttpMethod.Get, eventUri);
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

    public async Task ReceivingEventUsingWebhookAsync(
        string webhookUrl,
        string? token,
        CancellationToken cancellationToken,
        TaskCompletionSource<bool>? started = null)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            throw new ArgumentException("WebhookUrl 不能为空。", nameof(webhookUrl));
        }

        using var listener = new HttpListener();
        listener.Prefixes.Add(NormalizeWebhookPrefix(webhookUrl));
        try
        {
            listener.Start();
            started?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            started?.TrySetException(ex);
            throw;
        }
        using var cancellationRegistration = cancellationToken.Register(
            static state => ((HttpListener)state!).Close(), listener);

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

            try
            {
                await HandleWebhookAsync(context, token, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                BotLog.Error($"Milky Webhook 请求处理异常: {ex.GetType().Name}: {ex.Message}");
            }
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

            using var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding ?? Encoding.UTF8,
                leaveOpen: true);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            var data = DeserializeEvent(payload, "Webhook", out var error);
            if (data is null)
            {
                await WriteResponseAsync(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    $"Invalid Milky event: {error ?? "empty payload"}",
                    cancellationToken);
                return;
            }

            await PublishIfNotNullAsync(data);
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

        var data = DeserializeEvent(payload.ToString(), "SSE");
        payload.Clear();
        await PublishIfNotNullAsync(data);
    }

    internal static Event? DeserializeEvent(string payload, string source) =>
        DeserializeEvent(payload, source, out _);

    internal static Event? DeserializeEvent(string payload, string source, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Milky event payload is empty.";
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Deserialize<Event>(JsonOptions);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            BotLog.Warning($"忽略无法解析的 Milky {source} 事件: {ex.Message}");
            return null;
        }
    }

    private async Task PublishIfNotNullAsync(Event? data)
    {
        if (data is UnknownMilkyEvent unknownEvent)
        {
            BotLog.Warning($"忽略未知 Milky 事件类型 '{unknownEvent.EventType}'。");
            return;
        }

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
        ["xml"] = typeof(XmlIncomingSegment),
        ["markdown"] = typeof(MarkdownIncomingSegment)
    };

    public override IncomingSegment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return CreateUnknownSegment("<missing>");
        }

        var segmentTypeName = typeElement.GetString();
        if (string.IsNullOrWhiteSpace(segmentTypeName) ||
            !WrappedSegmentTypes.TryGetValue(segmentTypeName, out var segmentType))
        {
            return CreateUnknownSegment(segmentTypeName ?? "<empty>");
        }

        if (!element.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Milky segment '{segmentTypeName}' missing object data.");
        }

        return dataElement.Deserialize(segmentType, options) as IncomingSegment
               ?? throw new JsonException($"Cannot deserialize Milky segment '{segmentTypeName}' as {segmentType.Name}.");
    }

    public override void Write(Utf8JsonWriter writer, IncomingSegment value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static TextIncomingSegment CreateUnknownSegment(string segmentType)
    {
        BotLog.Warning($"收到未知 Milky 消息段类型 '{segmentType}'，已转换为诊断文本。");
        return new TextIncomingSegment($"[Unsupported Milky segment type: {segmentType}]");
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
        ["group_disband"] = typeof(GroupDisbandEvent),
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

        var eventType = root.TryGetProperty("event_type", out var eventTypeElement) && eventTypeElement.ValueKind == JsonValueKind.String
            ? eventTypeElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new JsonException("Milky event payload missing event_type.");
        }

        if (string.Equals(eventType, "message_receive", StringComparison.OrdinalIgnoreCase))
        {
            var payload = CreateRequiredEventPayload(root, eventType);
            return DeserializeMessageReceive(root, payload, options);
        }

        if (EventTypes.TryGetValue(eventType, out var targetType))
        {
            var payload = CreateRequiredEventPayload(root, eventType);
            return DeserializeRequired(payload, targetType, options, eventType);
        }

        return new UnknownMilkyEvent(eventType, root.Clone());
    }

    public override void Write(Utf8JsonWriter writer, Event value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static Event DeserializeMessageReceive(JsonElement root, JsonElement payload, JsonSerializerOptions options)
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

        return new UnknownMilkyEvent($"message_receive/{scene ?? "<missing>"}", root.Clone());
    }

    private static JsonElement CreateRequiredEventPayload(JsonElement root, string eventType)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Milky event '{eventType}' missing object data.");
        }

        var merged = new JsonObject();
        foreach (var property in root.EnumerateObject())
        {
            if (!property.NameEquals("data") && !property.NameEquals("event_type"))
            {
                merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        foreach (var property in data.EnumerateObject())
        {
            merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }

        using var document = JsonDocument.Parse(merged.ToJsonString());
        return document.RootElement.Clone();
    }

    private static Event DeserializeRequired(JsonElement payload, Type targetType, JsonSerializerOptions options, string eventType)
    {
        var result = payload.Deserialize(targetType, options) as Event;
        return result ?? throw new JsonException($"Cannot deserialize Milky event '{eventType}' as {targetType.Name}.");
    }
}

internal sealed record UnknownMilkyEvent(string EventType, JsonElement Payload) : Event;
