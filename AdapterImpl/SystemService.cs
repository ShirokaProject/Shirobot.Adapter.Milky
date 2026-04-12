using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.System.Requests;
using ShiroBot.Model.System.Responses;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class SystemService : ISystemService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task<GetLoginInfoResponse> GetLoginInfoAsync() =>
        Milky.RequestAsync<GetLoginInfoRequest, GetLoginInfoResponse>(new GetLoginInfoRequest());

    public Task<GetImplInfoResponse> GetImplInfoAsync() =>
        Milky.RequestAsync<GetImplInfoRequest, GetImplInfoResponse>(new GetImplInfoRequest());

    public Task<GetUserProfileResponse> GetUserProfileAsync(GetUserProfileRequest request) =>
        Milky.RequestAsync<GetUserProfileRequest, GetUserProfileResponse>(request);

    public Task<GetFriendListResponse> GetFriendListAsync(GetFriendListRequest request) =>
        Milky.RequestAsync<GetFriendListRequest, GetFriendListResponse>(request);

    public Task<GetFriendInfoResponse> GetFriendInfoAsync(GetFriendInfoRequest request) =>
        Milky.RequestAsync<GetFriendInfoRequest, GetFriendInfoResponse>(request);

    public Task<GetGroupListResponse> GetGroupListAsync(GetGroupListRequest request) =>
        Milky.RequestAsync<GetGroupListRequest, GetGroupListResponse>(request);

    public Task<GetGroupInfoResponse> GetGroupInfoAsync(GetGroupInfoRequest request) =>
        Milky.RequestAsync<GetGroupInfoRequest, GetGroupInfoResponse>(request);

    public Task<GetGroupMemberListResponse> GetGroupMemberListAsync(GetGroupMemberListRequest request) =>
        Milky.RequestAsync<GetGroupMemberListRequest, GetGroupMemberListResponse>(request);

    public Task<GetGroupMemberInfoResponse> GetGroupMemberInfoAsync(GetGroupMemberInfoRequest request) =>
        Milky.RequestAsync<GetGroupMemberInfoRequest, GetGroupMemberInfoResponse>(request);

    public Task SetAvatarAsync(SetAvatarRequest request) =>
        Milky.RequestAsync(request);

    public Task SetNicknameAsync(SetNicknameRequest request) =>
        Milky.RequestAsync(request);

    public Task SetBioAsync(SetBioRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetCustomFaceUrlListResponse> GetCustomFaceUrlListAsync() =>
        Milky.RequestAsync<GetCustomFaceUrlListRequest, GetCustomFaceUrlListResponse>(new GetCustomFaceUrlListRequest());

    public Task<GetCookiesResponse> GetCookiesAsync(GetCookiesRequest request) =>
        Milky.RequestAsync<GetCookiesRequest, GetCookiesResponse>(request);

    public Task<GetCsrfTokenResponse> GetCsrfTokenAsync() =>
        Milky.RequestAsync<GetCsrfTokenRequest, GetCsrfTokenResponse>(new GetCsrfTokenRequest());
}

internal sealed record GetLoginInfoRequest();

internal sealed record GetImplInfoRequest();

internal sealed record GetCustomFaceUrlListRequest();

internal sealed record GetCsrfTokenRequest();
