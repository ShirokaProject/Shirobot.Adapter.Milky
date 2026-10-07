using System.Net;
using System.Text.Json;
using ShiroBot.Adapter.Milky.AdapterImpl;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;
using Xunit;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class GenericMessageTests
{
    private static readonly Channel Group = new("123", ChannelType.Group);
    [Fact]
    public async Task ExplicitMarkdownFallbackReachesWireAndReportsTransformation()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        IMessageService service = new CoreMessageService(client);
        var message = new OutgoingMessage { Segments = [new MarkdownSegment("**title**") { PlainTextFallback = "title" }] };
        Assert.False(service.AssessMessage(Group, message).IsSupported);
        message = message with { AllowedFallbacks = MessageFallbackOptions.MarkdownAsText };
        var assessment = service.AssessMessage(Group, message);
        Assert.True(assessment.IsSupported);
        Assert.False(assessment.IsNative);
        var result = await service.SendMessageAsync(Group, message);
        Assert.Equal("9", result.MessageId);
        Assert.Equal(MessageTransformationKind.MarkdownToText, Assert.Single(result.Transformations).Kind);
        using var json = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.Equal("title", json.RootElement.GetProperty("message")[0].GetProperty("data").GetProperty("text").GetString());
    }
    [Fact]
    public void LinkFallbackRetainsUrlButCallbackCannotBeDiscarded()
    {
        IMessageService service = new CoreMessageService();
        var request = new OutgoingMessage { Segments = [new TextSegment("body")],
            Buttons = new([new([new("visit", "site", new OpenUrlAction("https://example.com/"))])]),
            AllowedFallbacks = MessageFallbackOptions.LinkButtonsAsText };
        var prepared = MessagePreparation.Prepare(request, service.GetMessageCapabilities(Group));
        Assert.Null(prepared.Message.Buttons);
        Assert.Contains("https://example.com/", string.Concat(prepared.Message.Segments.OfType<TextSegment>().Select(x => x.Text)));
        var callbacks = request with { Buttons = new([new([new("click", "click", new CallbackAction("data"))])]) };
        Assert.False(service.AssessMessage(Group, callbacks).IsSupported);
    }
    [Fact]
    public void CardFallbackRetainsFieldsAndRejectsMultipleQuotes()
    {
        IMessageService service = new CoreMessageService();
        var request = new OutgoingMessage { Segments = [new CardSegment { Title = "title", Fields = [new("field", "value")] }], AllowedFallbacks = MessageFallbackOptions.CardAsText };
        var prepared = MessagePreparation.Prepare(request, service.GetMessageCapabilities(Group));
        Assert.Contains("field: value", Assert.IsType<TextSegment>(Assert.Single(prepared.Message.Segments)).Text);
        Assert.False(service.AssessMessage(Group, request with { AllowedFallbacks = MessageFallbackOptions.None }).IsSupported);
        Assert.False(service.AssessMessage(Group, new() { Segments = [new QuoteSegment("1"), new QuoteSegment("2"), new TextSegment("body")] }).IsSupported);
    }
    [Fact]
    public async Task ReactionPreservesUnicodeAndRejectsForeignPlatformEmoji()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        IMessageReactionService service = new QGroupApi(client);
        var reference = new MessageReference("milky-a", Group, "9");
        await service.SetReactionAsync(reference, new UnicodeReactionEmoji("👍"));
        using var json = JsonDocument.Parse(Assert.Single(handler.Bodies));
        Assert.Equal("👍", json.RootElement.GetProperty("reaction").GetString());
        Assert.Equal("emoji", json.RootElement.GetProperty("reaction_type").GetString());
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetReactionAsync(reference, new PlatformReactionEmoji("1", "milky-b")));
        Assert.Single(handler.Bodies);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SetReactionAsync(reference, new UnicodeReactionEmoji("👍"), cancellationToken: canceled.Token));
        Assert.Single(handler.Bodies);
    }
    [Fact]
    public void IncomingReactionUsesCommonEventAndPreservesQqPayload()
    {
        var wire = JsonSerializer.Deserialize<ShiroBot.Adapter.Milky.Model.Common.Event>("""
            {"event_type":"group_message_reaction","time":1,"self_id":2,
             "data":{"group_id":3,"user_id":4,"message_seq":5,"face_id":"👍","reaction_type":"emoji","is_add":true}}
            """, MilkyJson.JsonOptions);
        Assert.NotNull(wire);
        var mapped = Assert.IsType<MessageReactionEvent>(MilkyMapper.ToBotEvent(wire));
        Assert.Equal("👍", Assert.IsType<UnicodeReactionEmoji>(mapped.Emoji).Value);
        Assert.Equal("5", mapped.MessageId);
        Assert.Equal("4", mapped.User!.Id);
        Assert.IsType<ShiroBot.Model.QQ.QGroupMessageReaction>(mapped.Raw);
        Assert.Null(mapped.Count);
    }
    private sealed class Handler : HttpMessageHandler
    {
        internal List<string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(token));
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"ok\",\"retcode\":0,\"data\":{\"message_seq\":9,\"time\":1}}") };
        }
    }
}
