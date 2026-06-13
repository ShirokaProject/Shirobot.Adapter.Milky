using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class MessageService : IMessageService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task<SendPrivateMessageResponse> SendPrivateMessageAsync(SendPrivateMessageRequest request) =>
        Milky.RequestAsync<SendPrivateMessageRequest, SendPrivateMessageResponse>(
            request with { Message = ResourceUriConverter.Convert(request.Message) });

    public Task<SendGroupMessageResponse> SendGroupMessageAsync(SendGroupMessageRequest request) =>
        Milky.RequestAsync<SendGroupMessageRequest, SendGroupMessageResponse>(
            request with { Message = ResourceUriConverter.Convert(request.Message) });

    public Task RecallPrivateMessageAsync(RecallPrivateMessageRequest request) =>
        Milky.RequestAsync(request);

    public Task RecallGroupMessageAsync(RecallGroupMessageRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetMessageResponse> GetMessageAsync(GetMessageRequest request) =>
        Milky.RequestAsync<GetMessageRequest, GetMessageResponse>(request);

    public Task<GetHistoryMessagesResponse> GetHistoryMessagesAsync(GetHistoryMessagesRequest request) =>
        Milky.RequestAsync<GetHistoryMessagesRequest, GetHistoryMessagesResponse>(request);

    public Task<GetResourceTempUrlResponse> GetResourceTempUrlAsync(GetResourceTempUrlRequest request) =>
        Milky.RequestAsync<GetResourceTempUrlRequest, GetResourceTempUrlResponse>(request);

    public Task<GetForwardedMessagesResponse> GetForwardedMessagesAsync(GetForwardedMessagesRequest request) =>
        Milky.RequestAsync<GetForwardedMessagesRequest, GetForwardedMessagesResponse>(request);

    public Task MarkMessageAsReadAsync(MarkMessageAsReadRequest request) =>
        Milky.RequestAsync(request);
}
