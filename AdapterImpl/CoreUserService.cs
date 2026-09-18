using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Adapter;
using Sdk = ShiroBot.SDK.Models;

namespace ShiroBot.Adapter.Milky.AdapterImpl;

/// <summary>平台无关用户/好友服务的 Milky 实现。</summary>
public class CoreUserService : IUserService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<Sdk.User> GetSelfAsync()
    {
        var response = await Milky.RequestAsync<GetLoginInfoRequest, GetLoginInfoResponse>(new GetLoginInfoRequest());
        return new Sdk.User(response.Uin.ToString()) { Name = response.Nickname, IsBot = true };
    }

    public async Task<Sdk.User?> GetUserAsync(string userId)
    {
        var response = await Milky.RequestAsync<GetUserProfileRequest, GetUserProfileResponse>(
            new GetUserProfileRequest(MilkyMapper.ParseId(userId, "userId")));
        return new Sdk.User(userId) { Name = response.Nickname };
    }

    public async Task<IReadOnlyList<Sdk.User>> GetFriendsAsync()
    {
        var response = await Milky.RequestAsync<GetFriendListRequest, GetFriendListResponse>(
            new GetFriendListRequest(false));
        return response.Friends.Select(MilkyMapper.ToUser).ToArray();
    }

    public Task AcceptFriendRequestAsync(string token) =>
        Milky.RequestAsync(new AcceptFriendRequestRequest(token, false));

    public Task RejectFriendRequestAsync(string token, string? reason = null) =>
        Milky.RequestAsync(new RejectFriendRequestRequest(token, false, reason));
}
