using System.Net;
using System.Text.Json;
using ShiroBot.Adapter.Milky.AdapterImpl;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;
using ShiroBot.Model.QQ;
using Xunit;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class GenericFileTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FileSegmentUsesUploadWithoutInventingMessageId(bool group)
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        var messages = new CoreMessageService(client);
        var channel = group ? Channel.Group("123") : Channel.Direct("123");
        Assert.True(messages.GetMessageCapabilities(channel).NativeFeatures.HasFlag(MessageFeatures.File));
        var sent = await messages.SendMessageAsync(channel, [new FileSegment("file:///tmp/report.pdf") { FileName = "report.pdf" }]);
        Assert.True(sent.IsSuccess);
        Assert.Empty(sent.MessageId);
        Assert.Null(sent.Reference);
        Assert.Equal("file-id", sent.UploadedFile!.FileId);
        Assert.True(sent.UploadedFile.IsPublished);
        Assert.EndsWith(group ? "upload_group_file" : "upload_private_file", handler.Route);
        Assert.Equal("report.pdf", JsonDocument.Parse(handler.Body!).RootElement.GetProperty("file_name").GetString());
        Assert.IsAssignableFrom<IQFileApi>(new QFileApi(client));
        Assert.IsAssignableFrom<IFileService>(new QFileApi(client));
    }

    [Fact]
    public async Task InvalidCombinationsAndCancellationDoNotUpload()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        var messages = new CoreMessageService(client);
        await Assert.ThrowsAsync<NotSupportedException>(() => messages.SendMessageAsync(Channel.Group("123"),
            [new TextSegment("caption"), new FileSegment("file:///tmp/report.pdf")]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new QFileApi(client).UploadAsync(Channel.Group("123"),
            new() { Uri = "file:///tmp/report.pdf", FileName = "report.pdf" }, cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => messages.SendMessageAsync(new Channel("123", ChannelType.Other),
            [new FileSegment("file:///tmp/report.pdf")]));
        Assert.Null(handler.Route);
    }

    private sealed class Handler : HttpMessageHandler
    {
        internal string? Route;
        internal string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Route = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(token);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"ok\",\"retcode\":0,\"data\":{\"file_id\":\"file-id\"}}") };
        }
    }
}
