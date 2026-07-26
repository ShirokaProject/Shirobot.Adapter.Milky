using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.Friend.Requests;
using ShiroBot.Model.Friend.Responses;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class FriendService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SendFriendNudgeAsync(SendFriendNudgeRequest request) =>
        Milky.RequestAsync(request);

    public Task SendProfileLikeAsync(SendProfileLikeRequest request) =>
        Milky.RequestAsync(request);

    public Task DeleteFriendAsync(DeleteFriendRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetFriendRequestsResponse> GetFriendRequestsAsync(GetFriendRequestsRequest request) =>
        Milky.RequestAsync<GetFriendRequestsRequest, GetFriendRequestsResponse>(request);

    public Task AcceptFriendRequestAsync(AcceptFriendRequestRequest request) =>
        Milky.RequestAsync(request);

    public Task RejectFriendRequestAsync(RejectFriendRequestRequest request) =>
        Milky.RequestAsync(request);
}
