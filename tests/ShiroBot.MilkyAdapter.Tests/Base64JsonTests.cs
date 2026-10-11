using System.Net;
using System.Text;
using System.Text.Json;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.Adapter.Milky.Model.Common;
using ShiroBot.Adapter.Milky.Model.Message.Requests;
using Xunit;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class Base64JsonTests
{
    [Fact]
    public async Task Base64AndEscapedTextReachTheHttpWireUnchanged()
    {
        // 0xfb repeatedly produces '+' and '/' in valid Base64.
        var bytes = Enumerable.Repeat((byte)0xfb, 1024 * 1024).ToArray();
        var uri = "base64://" + Convert.ToBase64String(bytes);
        const string text = "中文 <script> & + \"quoted\" \\path\n\t\0 😀";
        using var handler = new CaptureHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        await client.RequestAsync(new SendGroupMessageRequest(123,
            [new ImageOutgoingSegment(uri), new TextOutgoingSegment(text)]));
        Assert.Equal("application/json", handler.ContentType);
        Assert.DoesNotContain("\\u002B", handler.Json, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(handler.Json);
        var message = json.RootElement.GetProperty("message");
        Assert.Equal("image", message[0].GetProperty("type").GetString());
        var received = message[0].GetProperty("data").GetProperty("uri").GetString()!;
        Assert.Equal(uri, received);
        Assert.Equal(bytes, Convert.FromBase64String(received["base64://".Length..]));
        Assert.Equal(text, message[1].GetProperty("data").GetProperty("text").GetString());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string Json { get; private set; } = "";
        public string? ContentType { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            ContentType = request.Content!.Headers.ContentType!.MediaType;
            Json = await request.Content.ReadAsStringAsync(token);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\",\"retcode\":0,\"data\":{}}", Encoding.UTF8, "application/json")
            };
        }
    }
}
