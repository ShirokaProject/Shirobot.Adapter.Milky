using System.Globalization;
using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.Adapter.Milky.Model.File.Requests;
using ShiroBot.Adapter.Milky.Model.File.Responses;
using ShiroBot.Adapter.Milky.Model.Friend.Requests;
using ShiroBot.Adapter.Milky.Model.Friend.Responses;
using ShiroBot.Adapter.Milky.Model.Group.Requests;
using ShiroBot.Adapter.Milky.Model.Group.Responses;
using ShiroBot.Adapter.Milky.Model.Message.Requests;
using ShiroBot.Adapter.Milky.Model.Message.Responses;
using ShiroBot.Adapter.Milky.Model.System.Requests;
using ShiroBot.Adapter.Milky.Model.System.Responses;
using ShiroBot.Model.QQ;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;
using Mk = ShiroBot.Adapter.Milky.Model.Common;

namespace ShiroBot.Adapter.Milky.AdapterImpl;

internal sealed record GetPeerPinsRequest;

/// <summary>IQFriendApi 的 Milky 实现。</summary>
public sealed class QFriendApi : IQFriendApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SendNudgeAsync(string userId, bool isSelf = false, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendFriendNudgeRequest(MilkyMapper.ParseId(userId, "userId"), isSelf), cancellationToken: cancellationToken);

    public Task SendProfileLikeAsync(string userId, int count = 1, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendProfileLikeRequest(MilkyMapper.ParseId(userId, "userId"), count), cancellationToken: cancellationToken);

    public Task DeleteFriendAsync(string userId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new DeleteFriendRequest(MilkyMapper.ParseId(userId, "userId")), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<QFriendRequest>> GetFriendRequestsAsync(int limit = 20, bool isFiltered = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetFriendRequestsRequest, GetFriendRequestsResponse>(
            new GetFriendRequestsRequest(limit, isFiltered), cancellationToken: cancellationToken);
        return response.Requests
            .Select(request => new QFriendRequest
            {
                Time = DateTimeOffset.FromUnixTimeSeconds(request.Time),
                InitiatorId = request.InitiatorId.ToString(CultureInfo.InvariantCulture),
                InitiatorUid = request.InitiatorUid,
                TargetUserId = request.TargetUserId.ToString(CultureInfo.InvariantCulture),
                TargetUserUid = request.TargetUserUid,
                State = ToQqState(request.State),
                Comment = string.IsNullOrEmpty(request.Comment) ? null : request.Comment,
                Via = string.IsNullOrEmpty(request.Via) ? null : request.Via,
                IsFiltered = request.IsFiltered
            })
            .ToArray();
    }

    public Task AcceptFriendRequestAsync(string initiatorUid, bool isFiltered = false, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new AcceptFriendRequestRequest(initiatorUid, isFiltered), cancellationToken: cancellationToken);

    public Task RejectFriendRequestAsync(string initiatorUid, bool isFiltered = false, string? reason = null, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new RejectFriendRequestRequest(initiatorUid, isFiltered, reason), cancellationToken: cancellationToken);

    private static QRequestState ToQqState(Mk.FriendRequestState state) => state switch
    {
        Mk.FriendRequestState.Accepted => QRequestState.Accepted,
        Mk.FriendRequestState.Rejected => QRequestState.Rejected,
        Mk.FriendRequestState.Ignored => QRequestState.Ignored,
        _ => QRequestState.Pending
    };
}

/// <summary>IQGroupApi 的 Milky 实现。</summary>
public sealed class QGroupApi(MilkyClient? client = null) : IQGroupApi, IMessageReactionService
{
    public QGroupCapabilities Capabilities => QGroupCapabilities.Rename | QGroupCapabilities.Avatar
        | QGroupCapabilities.MemberCard | QGroupCapabilities.MemberTitle | QGroupCapabilities.MemberAdmin
        | QGroupCapabilities.MemberMute | QGroupCapabilities.WholeMute | QGroupCapabilities.Kick
        | QGroupCapabilities.Quit | QGroupCapabilities.Nudge | QGroupCapabilities.Reaction
        | QGroupCapabilities.Announcement | QGroupCapabilities.Essence | QGroupCapabilities.JoinRequests
        | QGroupCapabilities.Notifications | QGroupCapabilities.Invitations | QGroupCapabilities.BatchMute
        | QGroupCapabilities.GroupList | QGroupCapabilities.GroupInfo | QGroupCapabilities.Members;
    private MilkyClient Milky => client ?? MilkyClientManager.Instance;

    public ReactionCapabilities GetReactionCapabilities(Channel channel) => channel.Type == ChannelType.Group
        ? ReactionCapabilities.Unicode | ReactionCapabilities.PlatformEmoji : ReactionCapabilities.None;

    public Task SetReactionAsync(MessageReference message, ReactionEmoji emoji, bool isAdd = true, CancellationToken cancellationToken = default)
    {
        if (message.Channel.Type != ChannelType.Group) throw new NotSupportedException("Milky reactions require a group message.");
        if (emoji is PlatformReactionEmoji custom)
        {
            if (!string.Equals(custom.InstanceId, message.InstanceId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Platform emoji belongs to a different instance.");
            if (custom.GuildId is not null && custom.GuildId != message.Channel.GuildId && custom.GuildId != message.Channel.Id) throw new ArgumentException("Platform emoji belongs to a different guild.");
            ArgumentException.ThrowIfNullOrWhiteSpace(custom.Id);
            return SendMessageReactionAsync(message.Channel.Id, message.MessageId, custom.Id, QReactionType.Face, isAdd, cancellationToken);
        }
        if (emoji is UnicodeReactionEmoji unicode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unicode.Value);
            if (System.Globalization.StringInfo.ParseCombiningCharacters(unicode.Value).Length != 1) throw new ArgumentException("Unicode reaction must contain one grapheme.");
            return SendMessageReactionAsync(message.Channel.Id, message.MessageId, unicode.Value, QReactionType.Emoji, isAdd, cancellationToken);
        }
        throw new NotSupportedException("Unknown reaction emoji kind.");
    }

    public async Task<IReadOnlyList<QGroup>> GetGroupListAsync(bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupListRequest, GetGroupListResponse>(
            new GetGroupListRequest(noCache), cancellationToken);
        return response.Groups.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QGroup> GetGroupInfoAsync(string groupId, bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupInfoRequest, GetGroupInfoResponse>(
            new GetGroupInfoRequest(MilkyMapper.ParseId(groupId, "groupId"), noCache), cancellationToken);
        return QModelMapper.ToQq(response.Group);
    }

    public async Task<IReadOnlyList<QGroupMember>> GetGroupMemberListAsync(string groupId, bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupMemberListRequest, GetGroupMemberListResponse>(
            new GetGroupMemberListRequest(MilkyMapper.ParseId(groupId, "groupId"), noCache), cancellationToken);
        return response.Members.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QGroupMember> GetGroupMemberInfoAsync(string groupId, string userId, bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupMemberInfoRequest, GetGroupMemberInfoResponse>(
            new GetGroupMemberInfoRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), noCache), cancellationToken);
        return QModelMapper.ToQq(response.Member);
    }

    public Task SetGroupNameAsync(string groupId, string name, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupNameRequest(MilkyMapper.ParseId(groupId, "groupId"), name), cancellationToken: cancellationToken);

    public Task SetGroupAvatarAsync(string groupId, string imageUri, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupAvatarRequest(MilkyMapper.ParseId(groupId, "groupId"), ResourceUriConverter.Convert(imageUri)), cancellationToken: cancellationToken);

    public Task SetMemberCardAsync(string groupId, string userId, string card, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupMemberCardRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), card), cancellationToken: cancellationToken);

    public Task SetMemberSpecialTitleAsync(string groupId, string userId, string title, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupMemberSpecialTitleRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), title), cancellationToken: cancellationToken);

    public Task SetMemberAdminAsync(string groupId, string userId, bool isSet = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupMemberAdminRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), isSet), cancellationToken: cancellationToken);

    public Task MuteMemberAsync(string groupId, string userId, TimeSpan duration, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupMemberMuteRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), (int)duration.TotalSeconds), cancellationToken: cancellationToken);

    public Task SetWholeMuteAsync(string groupId, bool isMute = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupWholeMuteRequest(MilkyMapper.ParseId(groupId, "groupId"), isMute), cancellationToken: cancellationToken);

    public Task KickMemberAsync(string groupId, string userId, bool rejectAddRequest = false, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new KickGroupMemberRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId"), rejectAddRequest), cancellationToken: cancellationToken);

    public Task QuitGroupAsync(string groupId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new QuitGroupRequest(MilkyMapper.ParseId(groupId, "groupId")), cancellationToken: cancellationToken);

    public Task SendNudgeAsync(string groupId, string userId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendGroupNudgeRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(userId, "userId")), cancellationToken: cancellationToken);

    public Task SendMessageReactionAsync(string groupId, string messageId, string faceId, bool isAdd = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendGroupMessageReactionRequest(
            MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(messageId, "messageId"), faceId, SendGroupMessageReactionRequestReactionType.Face, isAdd), cancellationToken: cancellationToken);

    public Task SendMessageReactionAsync(string groupId, string messageId, string reactionId, QReactionType reactionType, bool isAdd = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendGroupMessageReactionRequest(
            MilkyMapper.ParseId(groupId, "groupId"),
            MilkyMapper.ParseId(messageId, "messageId"),
            reactionId,
            reactionType == QReactionType.Emoji
                ? SendGroupMessageReactionRequestReactionType.Emoji
                : SendGroupMessageReactionRequestReactionType.Face,
            isAdd), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<QGroupAnnouncement>> GetAnnouncementsAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupAnnouncementsRequest, GetGroupAnnouncementsResponse>(
            new GetGroupAnnouncementsRequest(MilkyMapper.ParseId(groupId, "groupId")), cancellationToken: cancellationToken);
        return response.Announcements
            .Select(announcement => new QGroupAnnouncement
            {
                GroupId = announcement.GroupId.ToString(CultureInfo.InvariantCulture),
                AnnouncementId = announcement.AnnouncementId,
                UserId = announcement.UserId.ToString(CultureInfo.InvariantCulture),
                Time = DateTimeOffset.FromUnixTimeSeconds(announcement.Time),
                Content = announcement.Content,
                ImageUrl = announcement.ImageUrl
            })
            .ToArray();
    }

    public Task SendAnnouncementAsync(string groupId, string content, string? imageUri = null, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SendGroupAnnouncementRequest(
            MilkyMapper.ParseId(groupId, "groupId"), content, imageUri is null ? null : ResourceUriConverter.Convert(imageUri)), cancellationToken: cancellationToken);

    public Task DeleteAnnouncementAsync(string groupId, string announcementId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new DeleteGroupAnnouncementRequest(MilkyMapper.ParseId(groupId, "groupId"), announcementId), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<QEssenceMessage>> GetEssenceMessagesAsync(string groupId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        => (await GetEssenceMessagesPageAsync(groupId, pageIndex, pageSize, cancellationToken)).Messages;

    public async Task<(IReadOnlyList<QEssenceMessage> Messages, bool IsEnd)> GetEssenceMessagesPageAsync(
        string groupId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupEssenceMessagesRequest, GetGroupEssenceMessagesResponse>(
            new GetGroupEssenceMessagesRequest(MilkyMapper.ParseId(groupId, "groupId"), pageIndex, pageSize), cancellationToken: cancellationToken);
        var messages = response.Messages
            .Select(message => new QEssenceMessage
            {
                GroupId = message.GroupId.ToString(CultureInfo.InvariantCulture),
                MessageId = message.MessageSeq.ToString(CultureInfo.InvariantCulture),
                MessageTime = DateTimeOffset.FromUnixTimeSeconds(message.MessageTime),
                SenderId = message.SenderId.ToString(CultureInfo.InvariantCulture),
                SenderName = message.SenderName,
                OperatorId = message.OperatorId.ToString(CultureInfo.InvariantCulture),
                OperatorName = message.OperatorName,
                OperationTime = DateTimeOffset.FromUnixTimeSeconds(message.OperationTime),
                Segments = QModelMapper.ToQq(message.Segments)
            })
            .ToArray();

        return (messages, response.IsEnd);
    }

    public Task SetEssenceMessageAsync(string groupId, string messageId, bool isSet = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetGroupEssenceMessageRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(messageId, "messageId"), isSet), cancellationToken: cancellationToken);

    public Task AcceptJoinRequestAsync(QGroupJoinRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Milky.RequestAsync(new AcceptGroupRequestRequest(
            MilkyMapper.ParseId(request.RequestId, "RequestId"),
            request.IsInvited ? AcceptGroupRequestRequestNotificationType.InvitedJoinRequest : AcceptGroupRequestRequestNotificationType.JoinRequest,
            MilkyMapper.ParseId(request.GroupId, "GroupId"), request.IsFiltered), cancellationToken);
    }

    public Task RejectJoinRequestAsync(QGroupJoinRequest request, string? reason = null, bool addToBlacklist = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (addToBlacklist) throw new NotSupportedException("Milky does not support rejecting and blacklisting in one operation.");
        return Milky.RequestAsync(new RejectGroupRequestRequest(
            MilkyMapper.ParseId(request.RequestId, "RequestId"),
            request.IsInvited ? RejectGroupRequestRequestNotificationType.InvitedJoinRequest : RejectGroupRequestRequestNotificationType.JoinRequest,
            MilkyMapper.ParseId(request.GroupId, "GroupId"), request.IsFiltered, reason), cancellationToken);
    }

    public async Task<QGroupJoinRequestPage> GetJoinRequestsAsync(string groupId, string? cursor = null,
        int limit = 20, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(limit));
        var groupNumber = MilkyMapper.ParseId(groupId, "GroupId");
        var response = await Milky.RequestAsync<GetGroupNotificationsRequest, GetGroupNotificationsResponse>(
            new GetGroupNotificationsRequest(cursor is null ? null : MilkyMapper.ParseId(cursor, "Cursor"), false, limit), cancellationToken);
        var requests = response.Notifications.Select(QModelMapper.ToJoinRequest)
            .Where(x => x is not null && x.GroupId == groupNumber.ToString(CultureInfo.InvariantCulture)).Cast<QGroupJoinRequest>().ToArray();
        return new QGroupJoinRequestPage(requests, response.NextNotificationSeq?.ToString(CultureInfo.InvariantCulture));
    }

    public Task<QBatchOperationResult> SetMemberMutesAsync(string groupId, IReadOnlyList<QMemberMute> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count is < 1 or > 20) throw new ArgumentException("Expected 1 to 20 members.", nameof(members));
        var groupNumber = MilkyMapper.ParseId(groupId, "GroupId");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            ArgumentNullException.ThrowIfNull(member);
            var numericId = MilkyMapper.ParseId(member.UserId, "UserId").ToString(CultureInfo.InvariantCulture);
            if (!ids.Add(numericId)) throw new ArgumentException("Duplicate user ID.", nameof(members));
            if (member.Duration < TimeSpan.Zero || member.Duration > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(members));
        }
        return QBatchMuteExecutor.ExecuteAsync(members,
            (member, token) => Milky.RequestAsync(new SetGroupMemberMuteRequest(groupNumber, MilkyMapper.ParseId(member.UserId, "UserId"), checked((int)member.Duration.TotalSeconds)), token), ex => ex is MilkyApiException, cancellationToken);
    }

    public async Task<(IReadOnlyList<QGroupNotification> Notifications, string? NextCursor)> GetNotificationsAsync(
        string? cursor = null, bool isFiltered = false, int limit = 20, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupNotificationsRequest, GetGroupNotificationsResponse>(
            new GetGroupNotificationsRequest(cursor is null ? null : MilkyMapper.ParseId(cursor, "Cursor"), isFiltered, limit), cancellationToken: cancellationToken);

        var notifications = response.Notifications
            .Select(ToQqNotification)
            .Where(notification => notification is not null)
            .Cast<QGroupNotification>()
            .ToArray();

        return (notifications, response.NextNotificationSeq?.ToString(CultureInfo.InvariantCulture));
    }

    public Task AcceptInvitationAsync(string groupId, string invitationId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new AcceptGroupInvitationRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(invitationId, "invitationId")), cancellationToken: cancellationToken);

    public Task RejectInvitationAsync(string groupId, string invitationId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new RejectGroupInvitationRequest(MilkyMapper.ParseId(groupId, "groupId"), MilkyMapper.ParseId(invitationId, "invitationId")), cancellationToken: cancellationToken);

    private static QGroupNotification? ToQqNotification(Mk.GroupNotification notification) => notification switch
    {
        Mk.JoinRequestGroupNotification join => new QJoinRequestNotification
        {
            GroupId = join.GroupId.ToString(CultureInfo.InvariantCulture),
            NotificationId = join.NotificationSeq.ToString(CultureInfo.InvariantCulture),
            InitiatorId = join.InitiatorId.ToString(CultureInfo.InvariantCulture),
            State = ToQqState(join.State),
            Comment = string.IsNullOrEmpty(join.Comment) ? null : join.Comment,
            IsFiltered = join.IsFiltered,
            OperatorId = join.OperatorId?.ToString(CultureInfo.InvariantCulture)
        },
        Mk.InvitedJoinRequestGroupNotification invited => new QInvitedJoinRequestNotification
        {
            GroupId = invited.GroupId.ToString(CultureInfo.InvariantCulture),
            NotificationId = invited.NotificationSeq.ToString(CultureInfo.InvariantCulture),
            InitiatorId = invited.InitiatorId.ToString(CultureInfo.InvariantCulture),
            TargetUserId = invited.TargetUserId.ToString(CultureInfo.InvariantCulture),
            State = invited.State switch
            {
                Mk.InvitedJoinRequestGroupNotificationState.Accepted => QRequestState.Accepted,
                Mk.InvitedJoinRequestGroupNotificationState.Rejected => QRequestState.Rejected,
                Mk.InvitedJoinRequestGroupNotificationState.Ignored => QRequestState.Ignored,
                _ => QRequestState.Pending
            },
            OperatorId = invited.OperatorId?.ToString(CultureInfo.InvariantCulture)
        },
        Mk.AdminChangeGroupNotification admin => new QAdminChangeNotification
        {
            GroupId = admin.GroupId.ToString(CultureInfo.InvariantCulture),
            NotificationId = admin.NotificationSeq.ToString(CultureInfo.InvariantCulture),
            TargetUserId = admin.TargetUserId.ToString(CultureInfo.InvariantCulture),
            IsSet = admin.IsSet,
            OperatorId = admin.OperatorId.ToString(CultureInfo.InvariantCulture)
        },
        Mk.KickGroupNotification kick => new QKickNotification
        {
            GroupId = kick.GroupId.ToString(CultureInfo.InvariantCulture),
            NotificationId = kick.NotificationSeq.ToString(CultureInfo.InvariantCulture),
            TargetUserId = kick.TargetUserId.ToString(CultureInfo.InvariantCulture),
            OperatorId = kick.OperatorId.ToString(CultureInfo.InvariantCulture)
        },
        Mk.QuitGroupNotification quit => new QQuitNotification
        {
            GroupId = quit.GroupId.ToString(CultureInfo.InvariantCulture),
            NotificationId = quit.NotificationSeq.ToString(CultureInfo.InvariantCulture),
            TargetUserId = quit.TargetUserId.ToString(CultureInfo.InvariantCulture)
        },
        _ => null
    };

    private static QRequestState ToQqState(Mk.JoinRequestGroupNotificationState state) => state switch
    {
        Mk.JoinRequestGroupNotificationState.Accepted => QRequestState.Accepted,
        Mk.JoinRequestGroupNotificationState.Rejected => QRequestState.Rejected,
        Mk.JoinRequestGroupNotificationState.Ignored => QRequestState.Ignored,
        _ => QRequestState.Pending
    };
}

/// <summary>IQFileApi 的 Milky 实现。</summary>
public sealed class QFileApi(MilkyClient? client = null) : IQFileApi, ShiroBot.SDK.Adapter.IFileService
{
    public QFileApi() : this(null) { }

    private MilkyClient Milky => client ?? MilkyClientManager.Instance;

    public ShiroBot.SDK.Models.FileCapabilities GetFileCapabilities(ShiroBot.SDK.Models.Channel channel) => new()
    {
        CanUpload = channel.Type is ShiroBot.SDK.Models.ChannelType.Group or ShiroBot.SDK.Models.ChannelType.Direct,
        UploadPublishes = true
    };

    public async Task<ShiroBot.SDK.Models.FileUploadResult> UploadAsync(ShiroBot.SDK.Models.Channel channel,
        ShiroBot.SDK.Models.FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        cancellationToken.ThrowIfCancellationRequested();
        if (!GetFileCapabilities(channel).CanUpload) throw new NotSupportedException("Milky file uploads support group and direct channels only.");
        var uri = Path.IsPathFullyQualified(request.Uri) ? new Uri(request.Uri).AbsoluteUri : request.Uri;
        var id = channel.Type == ShiroBot.SDK.Models.ChannelType.Group
            ? await UploadGroupFileAsync(channel.Id, uri, request.FileName, cancellationToken: cancellationToken).ConfigureAwait(false)
            : await UploadPrivateFileAsync(channel.Id, uri, request.FileName, cancellationToken).ConfigureAwait(false);
        return new() { FileId = id, IsPublished = true };
    }

    public async Task<string> UploadPrivateFileAsync(string userId, string fileUri, string fileName, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<UploadPrivateFileRequest, UploadPrivateFileResponse>(
            new UploadPrivateFileRequest(MilkyMapper.ParseId(userId, "userId"), ResourceUriConverter.Convert(fileUri), fileName), cancellationToken: cancellationToken);
        return response.FileId;
    }

    public async Task<string> UploadGroupFileAsync(string groupId, string fileUri, string fileName, string parentFolderId = "/", CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<UploadGroupFileRequest, UploadGroupFileResponse>(
            new UploadGroupFileRequest(MilkyMapper.ParseId(groupId, "groupId"), ResourceUriConverter.Convert(fileUri), fileName, parentFolderId), cancellationToken: cancellationToken);
        return response.FileId;
    }

    public async Task<string> GetPrivateFileDownloadUrlAsync(string userId, string fileId, string fileHash, CancellationToken cancellationToken = default)
        => await GetPrivateFileDownloadUrlAsync(userId, fileId, fileHash, false);

    public async Task<string> GetPrivateFileDownloadUrlAsync(
        string userId, string fileId, string fileHash, bool isSelfSend, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetPrivateFileDownloadUrlRequest, GetPrivateFileDownloadUrlResponse>(
            new GetPrivateFileDownloadUrlRequest(MilkyMapper.ParseId(userId, "userId"), fileId, fileHash, isSelfSend), cancellationToken: cancellationToken);
        return response.DownloadUrl;
    }

    public async Task<string> GetGroupFileDownloadUrlAsync(string groupId, string fileId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupFileDownloadUrlRequest, GetGroupFileDownloadUrlResponse>(
            new GetGroupFileDownloadUrlRequest(MilkyMapper.ParseId(groupId, "groupId"), fileId), cancellationToken: cancellationToken);
        return response.DownloadUrl;
    }

    public async Task<(IReadOnlyList<QGroupFile> Files, IReadOnlyList<QGroupFolder> Folders)> GetGroupFilesAsync(
        string groupId, string parentFolderId = "/", CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetGroupFilesRequest, GetGroupFilesResponse>(
            new GetGroupFilesRequest(MilkyMapper.ParseId(groupId, "groupId"), parentFolderId), cancellationToken: cancellationToken);

        var files = response.Files
            .Select(file => new QGroupFile
            {
                GroupId = file.GroupId.ToString(CultureInfo.InvariantCulture),
                FileId = file.FileId,
                FileName = file.FileName,
                ParentFolderId = file.ParentFolderId,
                FileSize = file.FileSize,
                UploadedTime = DateTimeOffset.FromUnixTimeSeconds(file.UploadedTime),
                UploaderId = file.UploaderId.ToString(CultureInfo.InvariantCulture),
                DownloadedTimes = file.DownloadedTimes,
                ExpireTime = file.ExpireTime is { } expire and > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(expire)
                    : null
            })
            .ToArray();

        var folders = response.Folders
            .Select(folder => new QGroupFolder
            {
                GroupId = folder.GroupId.ToString(CultureInfo.InvariantCulture),
                FolderId = folder.FolderId,
                ParentFolderId = folder.ParentFolderId,
                FolderName = folder.FolderName,
                CreatedTime = DateTimeOffset.FromUnixTimeSeconds(folder.CreatedTime),
                LastModifiedTime = DateTimeOffset.FromUnixTimeSeconds(folder.LastModifiedTime),
                CreatorId = folder.CreatorId.ToString(CultureInfo.InvariantCulture),
                FileCount = folder.FileCount
            })
            .ToArray();

        return (files, folders);
    }

    public Task MoveGroupFileAsync(string groupId, string fileId, string targetFolderId, string parentFolderId = "/", CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new MoveGroupFileRequest(MilkyMapper.ParseId(groupId, "groupId"), fileId, targetFolderId, parentFolderId), cancellationToken: cancellationToken);

    public Task RenameGroupFileAsync(string groupId, string fileId, string newFileName, string parentFolderId = "/", CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new RenameGroupFileRequest(MilkyMapper.ParseId(groupId, "groupId"), fileId, newFileName, parentFolderId), cancellationToken: cancellationToken);

    public Task DeleteGroupFileAsync(string groupId, string fileId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new DeleteGroupFileRequest(MilkyMapper.ParseId(groupId, "groupId"), fileId), cancellationToken: cancellationToken);

    public async Task<string> CreateGroupFolderAsync(string groupId, string folderName, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<CreateGroupFolderRequest, CreateGroupFolderResponse>(
            new CreateGroupFolderRequest(MilkyMapper.ParseId(groupId, "groupId"), folderName), cancellationToken: cancellationToken);
        return response.FolderId;
    }

    public Task RenameGroupFolderAsync(string groupId, string folderId, string newFolderName, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new RenameGroupFolderRequest(MilkyMapper.ParseId(groupId, "groupId"), folderId, newFolderName), cancellationToken: cancellationToken);

    public Task DeleteGroupFolderAsync(string groupId, string folderId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new DeleteGroupFolderRequest(MilkyMapper.ParseId(groupId, "groupId"), folderId), cancellationToken: cancellationToken);

    public Task PersistGroupFileAsync(string groupId, string fileId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new PersistGroupFileRequest(MilkyMapper.ParseId(groupId, "groupId"), fileId), cancellationToken: cancellationToken);
}

/// <summary>IQSystemApi 的 Milky 实现。</summary>
public sealed class QSystemApi : IQSystemApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<QUserProfile> GetUserProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetUserProfileRequest, GetUserProfileResponse>(
            new GetUserProfileRequest(MilkyMapper.ParseId(userId, "userId")), cancellationToken: cancellationToken);
        return new QUserProfile
        {
            UserId = userId,
            Nickname = response.Nickname,
            Qid = string.IsNullOrEmpty(response.Qid) ? null : response.Qid,
            Age = response.Age,
            Sex = response.Sex switch
            {
                GetUserProfileResponseSex.Male => QSex.Male,
                GetUserProfileResponseSex.Female => QSex.Female,
                _ => QSex.Unknown
            },
            Remark = string.IsNullOrEmpty(response.Remark) ? null : response.Remark,
            Bio = string.IsNullOrEmpty(response.Bio) ? null : response.Bio,
            Level = response.Level,
            Country = string.IsNullOrEmpty(response.Country) ? null : response.Country,
            City = string.IsNullOrEmpty(response.City) ? null : response.City,
            School = string.IsNullOrEmpty(response.School) ? null : response.School
        };
    }

    public async Task<IReadOnlyList<QFriend>> GetFriendListAsync(bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetFriendListRequest, GetFriendListResponse>(
            new GetFriendListRequest(noCache), cancellationToken: cancellationToken);
        return response.Friends.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QFriend> GetFriendInfoAsync(string userId, bool noCache = false, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetFriendInfoRequest, GetFriendInfoResponse>(
            new GetFriendInfoRequest(MilkyMapper.ParseId(userId, "userId"), noCache), cancellationToken: cancellationToken);
        return QModelMapper.ToQq(response.Friend);
    }

    public async Task<(IReadOnlyList<QFriend> Friends, IReadOnlyList<QGroup> Groups)> GetPeerPinsAsync(CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetPeerPinsRequest, GetPeerPinsResponse>(new GetPeerPinsRequest(), cancellationToken: cancellationToken);
        return (
            response.Friends.Select(QModelMapper.ToQq).ToArray(),
            response.Groups.Select(QModelMapper.ToQq).ToArray());
    }

    public Task SetAvatarAsync(string imageUri, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetAvatarRequest(ResourceUriConverter.Convert(imageUri)), cancellationToken: cancellationToken);

    public Task SetNicknameAsync(string nickname, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetNicknameRequest(nickname), cancellationToken: cancellationToken);

    public Task SetBioAsync(string bio, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetBioRequest(bio), cancellationToken: cancellationToken);

    public async Task<string> GetCookiesAsync(string domain, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetCookiesRequest, GetCookiesResponse>(new GetCookiesRequest(domain), cancellationToken: cancellationToken);
        return response.Cookies;
    }

    public async Task<string> GetCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetCsrfTokenRequest, GetCsrfTokenResponse>(new GetCsrfTokenRequest(), cancellationToken: cancellationToken);
        return response.CsrfToken;
    }

    async Task<QLoginInfo> IQSystemApi.GetLoginInfoAsync(CancellationToken cancellationToken)
    {
        var response = await Milky.RequestAsync<GetLoginInfoRequest, GetLoginInfoResponse>(new GetLoginInfoRequest(), cancellationToken: cancellationToken);
        return new QLoginInfo(response.Uin.ToString(CultureInfo.InvariantCulture), response.Nickname);
    }

    async Task<QImplInfo> IQSystemApi.GetImplInfoAsync(CancellationToken cancellationToken)
    {
        var response = await Milky.RequestAsync<GetImplInfoRequest, GetImplInfoResponse>(new GetImplInfoRequest(), cancellationToken: cancellationToken);
        return new QImplInfo
        {
            ImplName = response.ImplName,
            ImplVersion = response.ImplVersion,
            QqProtocolVersion = response.QqProtocolVersion,
            QqProtocolType = response.QqProtocolType.ToString().ToLowerInvariant(),
            ProtocolVersion = response.MilkyVersion
        };
    }

    async Task<IReadOnlyList<string>> IQSystemApi.GetCustomFaceUrlListAsync(CancellationToken cancellationToken)
    {
        var response = await Milky.RequestAsync<GetCustomFaceUrlListRequest, GetCustomFaceUrlListResponse>(
            new GetCustomFaceUrlListRequest(), cancellationToken: cancellationToken);
        return response.Urls;
    }

    public Task SetPeerPinAsync(QMessageScene scene, string peerId, bool isPinned = true, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new SetPeerPinRequest(
            scene switch
            {
                QMessageScene.Group => SetPeerPinRequestMessageScene.Group,
                QMessageScene.Temp => SetPeerPinRequestMessageScene.Temp,
                _ => SetPeerPinRequestMessageScene.Friend
            },
            MilkyMapper.ParseId(peerId, "peerId"),
            isPinned), cancellationToken: cancellationToken);
}

/// <summary>IQMessageApi 的 Milky 实现。</summary>
public sealed class QMessageApi : IQMessageApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<string> SendMessageAsync(
        QMessageScene scene,
        string peerId,
        IReadOnlyList<QOutgoingSegment> segments, CancellationToken cancellationToken = default)
        => (await SendMessageDetailedAsync(scene, peerId, segments, cancellationToken)).MessageId;

    public async Task<QSentMessage> SendMessageDetailedAsync(
        QMessageScene scene,
        string peerId,
        IReadOnlyList<QOutgoingSegment> segments, CancellationToken cancellationToken = default)
    {
        var milkySegments = ResourceUriConverter.Convert(segments.Select(QModelMapper.ToMilky).ToArray());

        if (scene == QMessageScene.Group)
        {
            var response = await Milky.RequestAsync<SendGroupMessageRequest, SendGroupMessageResponse>(
                new SendGroupMessageRequest(MilkyMapper.ParseId(peerId, "peerId"), milkySegments), cancellationToken: cancellationToken);
            return new QSentMessage(response.MessageSeq.ToString(CultureInfo.InvariantCulture), DateTimeOffset.FromUnixTimeSeconds(response.Time));
        }

        var privateResponse = await Milky.RequestAsync<SendPrivateMessageRequest, SendPrivateMessageResponse>(
            new SendPrivateMessageRequest(MilkyMapper.ParseId(peerId, "peerId"), milkySegments), cancellationToken: cancellationToken);
        return new QSentMessage(
            privateResponse.MessageSeq.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.FromUnixTimeSeconds(privateResponse.Time));
    }

    public async Task<QIncomingMessage?> GetMessageAsync(QMessageScene scene, string peerId, string messageId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetMessageRequest, GetMessageResponse>(new GetMessageRequest(
            scene switch
            {
                QMessageScene.Group => GetMessageRequestMessageScene.Group,
                QMessageScene.Temp => GetMessageRequestMessageScene.Temp,
                _ => GetMessageRequestMessageScene.Friend
            },
            MilkyMapper.ParseId(peerId, "peerId"),
            MilkyMapper.ParseId(messageId, "messageId")), cancellationToken: cancellationToken);
        return QModelMapper.ToQq(response.Message);
    }

    public async Task<(IReadOnlyList<QIncomingMessage> Messages, string? NextCursor)> GetHistoryMessagesAsync(
        QMessageScene scene, string peerId, string? cursor = null, int limit = 20, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetHistoryMessagesRequest, GetHistoryMessagesResponse>(
            new GetHistoryMessagesRequest(
                scene switch
                {
                    QMessageScene.Group => GetHistoryMessagesRequestMessageScene.Group,
                    QMessageScene.Temp => GetHistoryMessagesRequestMessageScene.Temp,
                    _ => GetHistoryMessagesRequestMessageScene.Friend
                },
                MilkyMapper.ParseId(peerId, "peerId"),
                cursor is null ? null : MilkyMapper.ParseId(cursor, "Cursor"),
                limit), cancellationToken: cancellationToken);

        var messages = response.Messages
            .Select(QModelMapper.ToQq)
            .Where(message => message is not null)
            .Cast<QIncomingMessage>()
            .ToArray();

        return (messages, response.NextMessageSeq?.ToString(CultureInfo.InvariantCulture));
    }

    public Task RecallMessageAsync(QMessageScene scene, string peerId, string messageId, CancellationToken cancellationToken = default) =>
        scene == QMessageScene.Group
            ? Milky.RequestAsync(new RecallGroupMessageRequest(MilkyMapper.ParseId(peerId, "peerId"), MilkyMapper.ParseId(messageId, "messageId")), cancellationToken: cancellationToken)
            : Milky.RequestAsync(new RecallPrivateMessageRequest(MilkyMapper.ParseId(peerId, "peerId"), MilkyMapper.ParseId(messageId, "messageId")), cancellationToken: cancellationToken);

    public async Task<string> GetResourceTempUrlAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetResourceTempUrlRequest, GetResourceTempUrlResponse>(
            new GetResourceTempUrlRequest(resourceId), cancellationToken: cancellationToken);
        return response.Url;
    }

    public async Task<IReadOnlyList<QForwardedIncomingMessage>> GetForwardedMessagesAsync(string forwardId, CancellationToken cancellationToken = default)
    {
        var response = await Milky.RequestAsync<GetForwardedMessagesRequest, GetForwardedMessagesResponse>(
            new GetForwardedMessagesRequest(forwardId), cancellationToken: cancellationToken);
        return response.Messages
            .Select(message => new QForwardedIncomingMessage
            {
                MessageId = message.MessageSeq.ToString(CultureInfo.InvariantCulture),
                SenderName = message.SenderName,
                AvatarUrl = message.AvatarUrl,
                Time = DateTimeOffset.FromUnixTimeSeconds(message.Time),
                Segments = QModelMapper.ToQq(message.Segments)
            })
            .ToArray();
    }

    public Task MarkAsReadAsync(QMessageScene scene, string peerId, string messageId, CancellationToken cancellationToken = default) =>
        Milky.RequestAsync(new MarkMessageAsReadRequest(
            scene switch
            {
                QMessageScene.Group => MarkMessageAsReadRequestMessageScene.Group,
                QMessageScene.Temp => MarkMessageAsReadRequestMessageScene.Temp,
                _ => MarkMessageAsReadRequestMessageScene.Friend
            },
            MilkyMapper.ParseId(peerId, "peerId"),
            MilkyMapper.ParseId(messageId, "messageId")), cancellationToken: cancellationToken);
}
