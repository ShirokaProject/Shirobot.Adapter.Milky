using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.Group.Requests;
using ShiroBot.Model.Group.Responses;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class GroupService : IGroupService
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SetGroupNameAsync(SetGroupNameRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupAvatarAsync(SetGroupAvatarRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupMemberCardAsync(SetGroupMemberCardRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupMemberSpecialTitleAsync(SetGroupMemberSpecialTitleRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupMemberAdminAsync(SetGroupMemberAdminRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupMemberMuteAsync(SetGroupMemberMuteRequest request) =>
        Milky.RequestAsync(request);

    public Task SetGroupWholeMuteAsync(SetGroupWholeMuteRequest request) =>
        Milky.RequestAsync(request);

    public Task KickGroupMemberAsync(KickGroupMemberRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetGroupAnnouncementsResponse> GetGroupAnnouncementsAsync(GetGroupAnnouncementsRequest request) =>
        Milky.RequestAsync<GetGroupAnnouncementsRequest, GetGroupAnnouncementsResponse>(request);

    public Task SendGroupAnnouncementAsync(SendGroupAnnouncementRequest request) =>
        Milky.RequestAsync(request);

    public Task DeleteGroupAnnouncementAsync(DeleteGroupAnnouncementRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetGroupEssenceMessagesResponse> GetGroupEssenceMessagesAsync(GetGroupEssenceMessagesRequest request) =>
        Milky.RequestAsync<GetGroupEssenceMessagesRequest, GetGroupEssenceMessagesResponse>(request);

    public Task SetGroupEssenceMessageAsync(SetGroupEssenceMessageRequest request) =>
        Milky.RequestAsync(request);

    public Task QuitGroupAsync(QuitGroupRequest request) =>
        Milky.RequestAsync(request);

    public Task SendGroupMessageReactionAsync(SendGroupMessageReactionRequest request) =>
        Milky.RequestAsync(request);

    public Task SendGroupNudgeAsync(SendGroupNudgeRequest request) =>
        Milky.RequestAsync(request);

    public Task<GetGroupNotificationsResponse> GetGroupNotificationsAsync(GetGroupNotificationsRequest request) =>
        Milky.RequestAsync<GetGroupNotificationsRequest, GetGroupNotificationsResponse>(request);

    public Task AcceptGroupRequestAsync(AcceptGroupRequestRequest request) =>
        Milky.RequestAsync(request);

    public Task RejectGroupRequestAsync(RejectGroupRequestRequest request) =>
        Milky.RequestAsync(request);

    public Task AcceptGroupInvitationAsync(AcceptGroupInvitationRequest request) =>
        Milky.RequestAsync(request);

    public Task RejectGroupInvitationAsync(RejectGroupInvitationRequest request) =>
        Milky.RequestAsync(request);
}
