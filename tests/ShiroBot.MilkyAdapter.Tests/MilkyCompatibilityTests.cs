using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ShiroBot.MilkyAdapter;
using ShiroBot.MilkyAdapter.AdapterImpl;
using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.Common;
using ShiroBot.Model.Group.Responses;
using ShiroBot.SDK.Core;
using Xunit;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class MilkyCompatibilityTests
{
    [Fact]
    public void Adapter_declares_sdk_070_metadata_attribute()
    {
        var attribute = typeof(MilkyAdapter).GetCustomAttribute<BotAdapterAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("MilkyAdapter", attribute.Id);
        Assert.Equal("Milky", attribute.Protocol);
        Assert.Equal(">=1.2.0 <1.4.0", attribute.ProtocolVersionRange);
    }

    [Fact]
    public void Markdown_segment_is_registered()
    {
        const string json = """
                            {"type":"markdown","data":{"content":"# title"}}
                            """;

        var segment = JsonSerializer.Deserialize<IncomingSegment>(json, MilkyJson.JsonOptions);

        var markdown = Assert.IsType<MarkdownIncomingSegment>(segment);
        Assert.Equal("# title", markdown.Content);
    }

    [Fact]
    public void Group_disband_event_is_registered_and_keeps_outer_metadata()
    {
        const string json = """
                            {
                              "event_type": "group_disband",
                              "time": 100,
                              "self_id": 200,
                              "data": {"group_id": 300, "operator_id": 400}
                            }
                            """;

        var value = JsonSerializer.Deserialize<Event>(json, MilkyJson.JsonOptions);

        var groupDisband = Assert.IsType<GroupDisbandEvent>(value);
        Assert.Equal(100, groupDisband.Time);
        Assert.Equal(200, groupDisband.SelfId);
        Assert.Equal(300, groupDisband.GroupId);
        Assert.Equal(400, groupDisband.OperatorId);
    }

    [Fact]
    public async Task Error_envelope_with_data_throws()
    {
        var client = CreateClient("""
                                  {"status":"failed","retcode":0,"message":"rejected","data":{"reason":"future"}}
                                  """);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RequestAsync<ProbeRequest, JsonElement>(new ProbeRequest("value")));

        Assert.Contains("status=failed", exception.Message);
    }

    [Fact]
    public async Task Error_envelope_without_data_throws()
    {
        var client = CreateClient("""
                                  {"status":"failed","retcode":-400,"message":"bad request"}
                                  """);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RequestAsync(new ProbeRequest("value")));

        Assert.Contains("retcode=-400", exception.Message);
    }

    [Fact]
    public async Task Json_element_response_returns_data_instead_of_envelope()
    {
        var client = CreateClient("""
                                  {
                                    "status":"ok",
                                    "retcode":0,
                                    "data":{"value":42},
                                    "future_envelope_field":"ignored"
                                  }
                                  """);

        var result = await client.RequestAsync<ProbeRequest, JsonElement>(new ProbeRequest("value"));

        Assert.Equal(42, result.GetProperty("value").GetInt32());
        Assert.False(result.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task New_response_fields_are_ignored()
    {
        var client = CreateClient("""
                                  {
                                    "status":"ok",
                                    "retcode":0,
                                    "data":{"value":42,"future_data_field":{"nested":true}},
                                    "future_envelope_field":"ignored"
                                  }
                                  """);

        var result = await client.RequestAsync<ProbeRequest, ProbeResponse>(new ProbeRequest("value"));

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task Request_json_omits_null_properties()
    {
        string? requestJson = null;
        var client = CreateClient(
            """{"status":"ok","retcode":0,"data":{}}""",
            content => requestJson = content);

        await client.RequestAsync(new ProbeRequest("value", null));

        Assert.NotNull(requestJson);
        using var document = JsonDocument.Parse(requestJson);
        Assert.Equal("value", document.RootElement.GetProperty("required").GetString());
        Assert.False(document.RootElement.TryGetProperty("optional", out _));
    }

    [Fact]
    public void Unknown_event_does_not_throw()
    {
        const string json = """
                            {"event_type":"future_event","time":1,"self_id":2,"data":{"value":3}}
                            """;

        var value = JsonSerializer.Deserialize<Event>(json, MilkyJson.JsonOptions);

        var unknown = Assert.IsType<UnknownMilkyEvent>(value);
        Assert.Equal("future_event", unknown.EventType);
    }

    [Fact]
    public void Unknown_or_missing_segment_type_becomes_diagnostic_text()
    {
        var unknown = JsonSerializer.Deserialize<IncomingSegment>(
            """{"type":"future_segment","data":{"value":3}}""",
            MilkyJson.JsonOptions);
        var missing = JsonSerializer.Deserialize<IncomingSegment>(
            """{"data":{"value":3}}""",
            MilkyJson.JsonOptions);

        var unknownText = Assert.IsType<TextIncomingSegment>(unknown);
        var missingText = Assert.IsType<TextIncomingSegment>(missing);
        Assert.Contains("future_segment", unknownText.Text);
        Assert.Contains("<missing>", missingText.Text);
    }

    [Fact]
    public void Known_segment_without_data_is_rejected()
    {
        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<IncomingSegment>("""{"type":"text"}""", MilkyJson.JsonOptions));

        Assert.Contains("missing object data", exception.Message);
    }

    [Fact]
    public void Known_event_without_data_is_rejected_by_safe_deserializer()
    {
        const string json = """
                            {"event_type":"group_disband","time":1,"self_id":2}
                            """;

        var value = MilkyEventHandler.DeserializeEvent(json, "test", out var error);

        Assert.Null(value);
        Assert.Contains("missing object data", error);
    }

    [Fact]
    public void Unknown_event_enum_is_diagnostic_but_does_not_escape_stream_processing()
    {
        const string json = """
                            {
                              "event_type":"group_message_reaction",
                              "time":1,
                              "self_id":2,
                              "data":{
                                "group_id":3,
                                "user_id":4,
                                "message_seq":5,
                                "face_id":"6",
                                "reaction_type":"future_reaction",
                                "is_add":true
                              }
                            }
                            """;

        var value = MilkyEventHandler.DeserializeEvent(json, "test");

        Assert.Null(value);
    }

    [Fact]
    public void Group_notification_converter_maps_all_current_types_and_skips_unknown_items()
    {
        const string json = """
                            {
                              "notifications": [
                                {
                                  "type":"join_request","group_id":1,"notification_seq":2,
                                  "is_filtered":false,"initiator_id":3,"state":"pending","comment":"hello"
                                },
                                {
                                  "type":"admin_change","group_id":4,"notification_seq":5,
                                  "target_user_id":6,"is_set":true,"operator_id":7
                                },
                                {
                                  "type":"future_notification","group_id":8,"notification_seq":9
                                },
                                {
                                  "type":"kick","group_id":10,"notification_seq":11,
                                  "target_user_id":12,"operator_id":13
                                },
                                {
                                  "type":"quit","group_id":14,"notification_seq":15,"target_user_id":16
                                },
                                {
                                  "type":"invited_join_request","group_id":17,"notification_seq":18,
                                  "initiator_id":19,"target_user_id":20,"state":"accepted"
                                }
                              ],
                              "next_notification_seq": 21
                            }
                            """;

        var response = JsonSerializer.Deserialize<GetGroupNotificationsResponse>(json, MilkyJson.JsonOptions);

        Assert.NotNull(response);
        Assert.Collection(
            response.Notifications,
            item => Assert.IsType<JoinRequestGroupNotification>(item),
            item => Assert.IsType<AdminChangeGroupNotification>(item),
            item => Assert.IsType<KickGroupNotification>(item),
            item => Assert.IsType<QuitGroupNotification>(item),
            item => Assert.IsType<InvitedJoinRequestGroupNotification>(item));
        Assert.Equal(21, response.NextNotificationSeq);

        var diagnostic = JsonSerializer.Deserialize<GroupNotification>(
            """{"type":"future_notification","value":1}""",
            MilkyJson.JsonOptions);
        Assert.Equal("future_notification", Assert.IsType<UnknownMilkyGroupNotification>(diagnostic).NotificationType);
    }

    [Fact]
    public async Task Webhook_returns_400_for_bad_events_continues_listening_and_cancels_pending_accept()
    {
        var port = GetAvailablePort();
        var webhookUrl = $"http://127.0.0.1:{port}/milky-test/";
        using var cancellationSource = new CancellationTokenSource();
        using var client = new HttpClient();
        var handler = new MilkyEventHandler(client);
        var received = new TaskCompletionSource<GroupDisbandEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.EventReceived += value =>
        {
            if (value is GroupDisbandEvent groupDisband)
            {
                received.TrySetResult(groupDisband);
            }

            return Task.CompletedTask;
        };

        var listenerTask = handler.ReceivingEventUsingWebhookAsync(webhookUrl, null, cancellationSource.Token);
        try
        {
            using var badJsonResponse = await client.PostAsync(
                webhookUrl,
                new StringContent("{", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, badJsonResponse.StatusCode);
            Assert.Contains("Invalid Milky event", await badJsonResponse.Content.ReadAsStringAsync());

            using var unknownEnumResponse = await client.PostAsync(
                webhookUrl,
                new StringContent(
                    """
                    {
                      "event_type":"group_message_reaction","time":1,"self_id":2,
                      "data":{
                        "group_id":3,"user_id":4,"message_seq":5,"face_id":"6",
                        "reaction_type":"future_reaction","is_add":true
                      }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, unknownEnumResponse.StatusCode);
            Assert.Contains("future_reaction", await unknownEnumResponse.Content.ReadAsStringAsync());

            using var validResponse = await client.PostAsync(
                webhookUrl,
                new StringContent(
                    """
                    {
                      "event_type":"group_disband","time":10,"self_id":20,
                      "data":{"group_id":30,"operator_id":40}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"));
            Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
            Assert.Equal(30, (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).GroupId);
        }
        finally
        {
            cancellationSource.Cancel();
            await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Event_service_awaits_each_multicast_subscriber()
    {
        var service = new EventService();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        service.MessageRecall += async _ =>
        {
            firstStarted.SetResult();
            await firstRelease.Task;
        };
        service.MessageRecall += async _ =>
        {
            secondStarted.SetResult();
            await secondRelease.Task;
        };

        var dispatch = service.OnEventReceivedAsync(new MessageRecallEvent(
            1,
            2,
            MessageRecallEventMessageScene.Group,
            3,
            4,
            5,
            6,
            "recalled"));

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(secondStarted.Task.IsCompleted);
        firstRelease.SetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(dispatch.IsCompleted);
        secondRelease.SetResult();
        await dispatch.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Version_parser_rejects_negative_parts_and_marks_prerelease_conservatively()
    {
        Assert.False(MilkyAdapter.TryParseMilkyVersion("-1.2.3", out _, out _));
        Assert.False(MilkyAdapter.TryParseMilkyVersion("1.-2.3", out _, out _));
        Assert.False(MilkyAdapter.TryParseMilkyVersion("1.2.-3", out _, out _));

        Assert.True(MilkyAdapter.TryParseMilkyVersion("v1.2.0-rc.1", out var version, out var isPreRelease));
        Assert.Equal(new Version(1, 2, 0), version);
        Assert.True(isPreRelease);
        Assert.True(MilkyAdapter.IsBelowMinimumMilkyVersion(version, isPreRelease));

        Assert.True(MilkyAdapter.TryParseMilkyVersion("1.3.0+build.1", out _, out isPreRelease));
        Assert.False(isPreRelease);
    }

    private static MilkyClient CreateClient(string responseJson, Action<string>? inspectRequest = null)
    {
        var handler = new StubHttpMessageHandler(async request =>
        {
            if (inspectRequest is not null)
            {
                inspectRequest(await request.Content!.ReadAsStringAsync());
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new MilkyClient(httpClient);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record ProbeRequest(string Required, string? Optional = null);

    private sealed record ProbeResponse(int Value);

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
