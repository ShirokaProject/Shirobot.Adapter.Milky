using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;
using Sdk = ShiroBot.SDK.Models;

namespace ShiroBot.Adapter.Milky.AdapterImpl;

/// <summary>平台无关群/成员服务的 Milky 实现。</summary>
public class CoreChannelService : IChannelService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<IReadOnlyList<Sdk.Channel>> GetChannelsAsync()
    {
        var response = await Milky.RequestAsync<GetGroupListRequest, GetGroupListResponse>(
            new GetGroupListRequest(false));
        return response.Groups.Select(MilkyMapper.ToChannel).ToArray();
    }

    public async Task<Sdk.Channel?> GetChannelAsync(string channelId)
    {
        var response = await Milky.RequestAsync<GetGroupInfoRequest, GetGroupInfoResponse>(
            new GetGroupInfoRequest(MilkyMapper.ParseId(channelId, "channelId"), false));
        return MilkyMapper.ToChannel(response.Group);
    }

    public async Task<IReadOnlyList<Member>> GetMembersAsync(string channelId)
    {
        var response = await Milky.RequestAsync<GetGroupMemberListRequest, GetGroupMemberListResponse>(
            new GetGroupMemberListRequest(MilkyMapper.ParseId(channelId, "channelId"), false));
        return response.Members.Select(MilkyMapper.ToMember).ToArray();
    }

    public async Task<Member?> GetMemberAsync(string channelId, string userId)
    {
        var response = await Milky.RequestAsync<GetGroupMemberInfoRequest, GetGroupMemberInfoResponse>(
            new GetGroupMemberInfoRequest(
                MilkyMapper.ParseId(channelId, "channelId"),
                MilkyMapper.ParseId(userId, "userId"),
                false));
        return MilkyMapper.ToMember(response.Member);
    }

    public Task SetChannelNameAsync(string channelId, string name) =>
        Milky.RequestAsync(new SetGroupNameRequest(MilkyMapper.ParseId(channelId, "channelId"), name));

    public Task KickMemberAsync(string channelId, string userId) =>
        Milky.RequestAsync(new KickGroupMemberRequest(
            MilkyMapper.ParseId(channelId, "channelId"),
            MilkyMapper.ParseId(userId, "userId"),
            false));

    public Task MuteMemberAsync(string channelId, string userId, TimeSpan duration) =>
        Milky.RequestAsync(new SetGroupMemberMuteRequest(
            MilkyMapper.ParseId(channelId, "channelId"),
            MilkyMapper.ParseId(userId, "userId"),
            (int)duration.TotalSeconds));

    public Task LeaveChannelAsync(string channelId) =>
        Milky.RequestAsync(new QuitGroupRequest(MilkyMapper.ParseId(channelId, "channelId")));
}
