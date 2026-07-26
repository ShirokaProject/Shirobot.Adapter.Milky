using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;
using Sdk = ShiroBot.SDK.Models;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

/// <summary>平台无关消息服务的 Milky 实现。</summary>
public class CoreMessageService : IMessageService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<SentMessage> SendMessageAsync(Sdk.Channel channel, IReadOnlyList<MessageSegment> segments)
    {
        var outgoing = ResourceUriConverter.Convert(MilkyMapper.ToOutgoingSegments(segments));

        if (channel.Type == ChannelType.Group)
        {
            var response = await Milky.RequestAsync<SendGroupMessageRequest, SendGroupMessageResponse>(
                new SendGroupMessageRequest(MilkyMapper.ParseId(channel.Id, "Channel.Id"), outgoing));
            return new SentMessage(response.MessageSeq.ToString())
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(response.Time)
            };
        }

        var privateResponse = await Milky.RequestAsync<SendPrivateMessageRequest, SendPrivateMessageResponse>(
            new SendPrivateMessageRequest(MilkyMapper.ParseId(channel.Id, "Channel.Id"), outgoing));
        return new SentMessage(privateResponse.MessageSeq.ToString())
        {
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(privateResponse.Time)
        };
    }

    public Task DeleteMessageAsync(Sdk.Channel channel, string messageId)
    {
        var seq = MilkyMapper.ParseId(messageId, "messageId");
        var peerId = MilkyMapper.ParseId(channel.Id, "Channel.Id");

        return channel.Type == ChannelType.Group
            ? Milky.RequestAsync(new RecallGroupMessageRequest(peerId, seq))
            : Milky.RequestAsync(new RecallPrivateMessageRequest(peerId, seq));
    }

    public async Task<MessageEvent?> GetMessageAsync(Sdk.Channel channel, string messageId)
    {
        var response = await Milky.RequestAsync<GetMessageRequest, GetMessageResponse>(new GetMessageRequest(
            ToMessageScene(channel),
            MilkyMapper.ParseId(channel.Id, "Channel.Id"),
            MilkyMapper.ParseId(messageId, "messageId")));

        return MilkyMapper.ToMessageEvent(response.Message);
    }

    public async Task<IReadOnlyList<MessageEvent>> GetHistoryMessagesAsync(
        Sdk.Channel channel,
        string? beforeMessageId = null,
        int limit = 20)
    {
        var response = await Milky.RequestAsync<GetHistoryMessagesRequest, GetHistoryMessagesResponse>(
            new GetHistoryMessagesRequest(
                channel.Type == ChannelType.Group
                    ? GetHistoryMessagesRequestMessageScene.Group
                    : channel.Type == ChannelType.Other
                        ? GetHistoryMessagesRequestMessageScene.Temp
                        : GetHistoryMessagesRequestMessageScene.Friend,
                MilkyMapper.ParseId(channel.Id, "Channel.Id"),
                beforeMessageId is null ? null : MilkyMapper.ParseId(beforeMessageId, "beforeMessageId"),
                limit));

        return response.Messages
            .Select(MilkyMapper.ToMessageEvent)
            .Where(message => message is not null)
            .Cast<MessageEvent>()
            .ToArray();
    }

    public async Task<string> GetResourceUrlAsync(string resourceId)
    {
        var response = await Milky.RequestAsync<GetResourceTempUrlRequest, GetResourceTempUrlResponse>(
            new GetResourceTempUrlRequest(resourceId));
        return response.Url;
    }

    private static GetMessageRequestMessageScene ToMessageScene(Sdk.Channel channel) => channel.Type switch
    {
        ChannelType.Group => GetMessageRequestMessageScene.Group,
        ChannelType.Other => GetMessageRequestMessageScene.Temp,
        _ => GetMessageRequestMessageScene.Friend
    };
}
