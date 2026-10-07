using System.Net;
using ShiroBot.Adapter.Milky.AdapterImpl;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.Model.QQ;
using Xunit;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class BatchAndCancellationTests
{
    [Fact]
    public async Task BatchContinuesAndRetainsUncertainOutcome()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        var api = new QGroupApi(client);
        handler.OnRequest = (_, _) => handler.Count == 2 ? throw new HttpRequestException("connection lost") : Task.CompletedTask;
        var result = await api.SetMemberMutesAsync("1", [
            new() { UserId = "2", Duration = TimeSpan.Zero }, new() { UserId = "3", Duration = TimeSpan.Zero }, new() { UserId = "4", Duration = TimeSpan.Zero }]);
        Assert.Equal(new[] { QOperationStatus.Succeeded, QOperationStatus.Unknown, QOperationStatus.Succeeded }, result.Items.Select(x => x.Status));
        Assert.False(result.IsSuccess);
        Assert.Equal(3, handler.Count);
    }

    [Fact]
    public async Task BatchValidatesAllIdsBeforeSendingAnything()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        var api = new QGroupApi(client);
        await Assert.ThrowsAsync<ArgumentException>(() => api.SetMemberMutesAsync("1", [
            new() { UserId = "2", Duration = TimeSpan.Zero }, new() { UserId = "openid", Duration = TimeSpan.Zero }]));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task BatchCancellationDistinguishesUnknownAndNotExecuted()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        using var cancel = new CancellationTokenSource();
        handler.OnRequest = (_, token) => { cancel.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; };
        var error = await Assert.ThrowsAsync<QBatchOperationCanceledException>(() => new QGroupApi(client).SetMemberMutesAsync("1", [
            new() { UserId = "2", Duration = TimeSpan.Zero }, new() { UserId = "3", Duration = TimeSpan.Zero }], cancel.Token));
        Assert.Equal(new[] { QOperationStatus.Unknown, QOperationStatus.NotExecuted }, error.PartialResult.Items.Select(x => x.Status));
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task CancellationReachesExistingGroupMutation()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        using var cancel = new CancellationTokenSource();
        handler.OnRequest = async (_, token) => { cancel.Cancel(); await Task.Delay(1000, token); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new QGroupApi(client).SetGroupNameAsync("1", "name", cancel.Token));
    }

    [Fact]
    public async Task ExplicitBusinessRejectionIsFailedRatherThanUnknown()
    {
        using var handler = new Handler { Response = "{\"status\":\"failed\",\"retcode\":400,\"message\":\"denied\",\"data\":{}}" };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new MilkyClient(http);
        var result = await new QGroupApi(client).SetMemberMutesAsync("1", [new() { UserId = "2", Duration = TimeSpan.Zero }]);
        Assert.Equal(QOperationStatus.Failed, Assert.Single(result.Items).Status);
    }

    private sealed class Handler : HttpMessageHandler
    {
        internal int Count;
        internal string Response = "{\"status\":\"ok\",\"retcode\":0,\"data\":{}}";
        internal Func<HttpRequestMessage, CancellationToken, Task>? OnRequest;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Count++;
            if (OnRequest is not null) await OnRequest(request, token);
            return new(HttpStatusCode.OK) { Content = new StringContent(Response) };
        }
    }
}
