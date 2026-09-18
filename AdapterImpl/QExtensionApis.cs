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
using Mk = ShiroBot.Adapter.Milky.Model.Common;

namespace ShiroBot.Adapter.Milky.AdapterImpl;

internal sealed record GetPeerPinsRequest;

/// <summary>IQFriendApi 的 Milky 实现。</summary>
public sealed class QFriendApi : IQFriendApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SendNudgeAsync(long userId, bool isSelf = false) =>
        Milky.RequestAsync(new SendFriendNudgeRequest(userId, isSelf));

    public Task SendProfileLikeAsync(long userId, int count = 1) =>
        Milky.RequestAsync(new SendProfileLikeRequest(userId, count));

    public Task DeleteFriendAsync(long userId) =>
        Milky.RequestAsync(new DeleteFriendRequest(userId));

    public async Task<IReadOnlyList<QFriendRequest>> GetFriendRequestsAsync(int limit = 20, bool isFiltered = false)
    {
        var response = await Milky.RequestAsync<GetFriendRequestsRequest, GetFriendRequestsResponse>(
            new GetFriendRequestsRequest(limit, isFiltered));
        return response.Requests
            .Select(request => new QFriendRequest
            {
                Time = DateTimeOffset.FromUnixTimeSeconds(request.Time),
                InitiatorId = request.InitiatorId,
                InitiatorUid = request.InitiatorUid,
                TargetUserId = request.TargetUserId,
                TargetUserUid = request.TargetUserUid,
                State = ToQqState(request.State),
                Comment = string.IsNullOrEmpty(request.Comment) ? null : request.Comment,
                Via = string.IsNullOrEmpty(request.Via) ? null : request.Via,
                IsFiltered = request.IsFiltered
            })
            .ToArray();
    }

    public Task AcceptFriendRequestAsync(string initiatorUid, bool isFiltered = false) =>
        Milky.RequestAsync(new AcceptFriendRequestRequest(initiatorUid, isFiltered));

    public Task RejectFriendRequestAsync(string initiatorUid, bool isFiltered = false, string? reason = null) =>
        Milky.RequestAsync(new RejectFriendRequestRequest(initiatorUid, isFiltered, reason));

    private static QRequestState ToQqState(Mk.FriendRequestState state) => state switch
    {
        Mk.FriendRequestState.Accepted => QRequestState.Accepted,
        Mk.FriendRequestState.Rejected => QRequestState.Rejected,
        Mk.FriendRequestState.Ignored => QRequestState.Ignored,
        _ => QRequestState.Pending
    };
}

/// <summary>IQGroupApi 的 Milky 实现。</summary>
public sealed class QGroupApi : IQGroupApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SetGroupNameAsync(long groupId, string name) =>
        Milky.RequestAsync(new SetGroupNameRequest(groupId, name));

    public Task SetGroupAvatarAsync(long groupId, string imageUri) =>
        Milky.RequestAsync(new SetGroupAvatarRequest(groupId, ResourceUriConverter.Convert(imageUri)));

    public Task SetMemberCardAsync(long groupId, long userId, string card) =>
        Milky.RequestAsync(new SetGroupMemberCardRequest(groupId, userId, card));

    public Task SetMemberSpecialTitleAsync(long groupId, long userId, string title) =>
        Milky.RequestAsync(new SetGroupMemberSpecialTitleRequest(groupId, userId, title));

    public Task SetMemberAdminAsync(long groupId, long userId, bool isSet = true) =>
        Milky.RequestAsync(new SetGroupMemberAdminRequest(groupId, userId, isSet));

    public Task MuteMemberAsync(long groupId, long userId, TimeSpan duration) =>
        Milky.RequestAsync(new SetGroupMemberMuteRequest(groupId, userId, (int)duration.TotalSeconds));

    public Task SetWholeMuteAsync(long groupId, bool isMute = true) =>
        Milky.RequestAsync(new SetGroupWholeMuteRequest(groupId, isMute));

    public Task KickMemberAsync(long groupId, long userId, bool rejectAddRequest = false) =>
        Milky.RequestAsync(new KickGroupMemberRequest(groupId, userId, rejectAddRequest));

    public Task QuitGroupAsync(long groupId) =>
        Milky.RequestAsync(new QuitGroupRequest(groupId));

    public Task SendNudgeAsync(long groupId, long userId) =>
        Milky.RequestAsync(new SendGroupNudgeRequest(groupId, userId));

    public Task SendMessageReactionAsync(long groupId, long messageSeq, string faceId, bool isAdd = true) =>
        Milky.RequestAsync(new SendGroupMessageReactionRequest(
            groupId, messageSeq, faceId, SendGroupMessageReactionRequestReactionType.Face, isAdd));

    public Task SendMessageReactionAsync(long groupId, long messageSeq, string reactionId, QReactionType reactionType, bool isAdd = true) =>
        Milky.RequestAsync(new SendGroupMessageReactionRequest(
            groupId,
            messageSeq,
            reactionId,
            reactionType == QReactionType.Emoji
                ? SendGroupMessageReactionRequestReactionType.Emoji
                : SendGroupMessageReactionRequestReactionType.Face,
            isAdd));

    public async Task<IReadOnlyList<QGroupAnnouncement>> GetAnnouncementsAsync(long groupId)
    {
        var response = await Milky.RequestAsync<GetGroupAnnouncementsRequest, GetGroupAnnouncementsResponse>(
            new GetGroupAnnouncementsRequest(groupId));
        return response.Announcements
            .Select(announcement => new QGroupAnnouncement
            {
                GroupId = announcement.GroupId,
                AnnouncementId = announcement.AnnouncementId,
                UserId = announcement.UserId,
                Time = DateTimeOffset.FromUnixTimeSeconds(announcement.Time),
                Content = announcement.Content,
                ImageUrl = announcement.ImageUrl
            })
            .ToArray();
    }

    public Task SendAnnouncementAsync(long groupId, string content, string? imageUri = null) =>
        Milky.RequestAsync(new SendGroupAnnouncementRequest(
            groupId, content, imageUri is null ? null : ResourceUriConverter.Convert(imageUri)));

    public Task DeleteAnnouncementAsync(long groupId, string announcementId) =>
        Milky.RequestAsync(new DeleteGroupAnnouncementRequest(groupId, announcementId));

    public async Task<IReadOnlyList<QEssenceMessage>> GetEssenceMessagesAsync(long groupId, int pageIndex, int pageSize)
        => (await GetEssenceMessagesPageAsync(groupId, pageIndex, pageSize)).Messages;

    public async Task<(IReadOnlyList<QEssenceMessage> Messages, bool IsEnd)> GetEssenceMessagesPageAsync(
        long groupId, int pageIndex, int pageSize)
    {
        var response = await Milky.RequestAsync<GetGroupEssenceMessagesRequest, GetGroupEssenceMessagesResponse>(
            new GetGroupEssenceMessagesRequest(groupId, pageIndex, pageSize));
        var messages = response.Messages
            .Select(message => new QEssenceMessage
            {
                GroupId = message.GroupId,
                MessageSeq = message.MessageSeq,
                MessageTime = DateTimeOffset.FromUnixTimeSeconds(message.MessageTime),
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                OperatorId = message.OperatorId,
                OperatorName = message.OperatorName,
                OperationTime = DateTimeOffset.FromUnixTimeSeconds(message.OperationTime),
                Segments = QModelMapper.ToQq(message.Segments)
            })
            .ToArray();

        return (messages, response.IsEnd);
    }

    public Task SetEssenceMessageAsync(long groupId, long messageSeq, bool isSet = true) =>
        Milky.RequestAsync(new SetGroupEssenceMessageRequest(groupId, messageSeq, isSet));

    public Task AcceptJoinRequestAsync(QGroupJoinRequest request) =>
        Milky.RequestAsync(new AcceptGroupRequestRequest(
            request.NotificationSeq,
            AcceptGroupRequestRequestNotificationType.JoinRequest,
            request.GroupId,
            request.IsFiltered));

    public Task RejectJoinRequestAsync(QGroupJoinRequest request, string? reason = null) =>
        Milky.RequestAsync(new RejectGroupRequestRequest(
            request.NotificationSeq,
            RejectGroupRequestRequestNotificationType.JoinRequest,
            request.GroupId,
            request.IsFiltered,
            reason));

    public Task AcceptJoinRequestAsync(long groupId, long notificationSeq, bool isInvited = false, bool isFiltered = false) =>
        Milky.RequestAsync(new AcceptGroupRequestRequest(
            notificationSeq,
            isInvited
                ? AcceptGroupRequestRequestNotificationType.InvitedJoinRequest
                : AcceptGroupRequestRequestNotificationType.JoinRequest,
            groupId,
            isFiltered));

    public Task RejectJoinRequestAsync(long groupId, long notificationSeq, bool isInvited = false, bool isFiltered = false, string? reason = null) =>
        Milky.RequestAsync(new RejectGroupRequestRequest(
            notificationSeq,
            isInvited
                ? RejectGroupRequestRequestNotificationType.InvitedJoinRequest
                : RejectGroupRequestRequestNotificationType.JoinRequest,
            groupId,
            isFiltered,
            reason));

    public async Task<(IReadOnlyList<QGroupNotification> Notifications, long? NextNotificationSeq)> GetNotificationsAsync(
        long? startNotificationSeq = null, bool isFiltered = false, int limit = 20)
    {
        var response = await Milky.RequestAsync<GetGroupNotificationsRequest, GetGroupNotificationsResponse>(
            new GetGroupNotificationsRequest(startNotificationSeq, isFiltered, limit));

        var notifications = response.Notifications
            .Select(ToQqNotification)
            .Where(notification => notification is not null)
            .Cast<QGroupNotification>()
            .ToArray();

        return (notifications, response.NextNotificationSeq);
    }

    public Task AcceptInvitationAsync(long groupId, long invitationSeq) =>
        Milky.RequestAsync(new AcceptGroupInvitationRequest(groupId, invitationSeq));

    public Task RejectInvitationAsync(long groupId, long invitationSeq) =>
        Milky.RequestAsync(new RejectGroupInvitationRequest(groupId, invitationSeq));

    private static QGroupNotification? ToQqNotification(Mk.GroupNotification notification) => notification switch
    {
        Mk.JoinRequestGroupNotification join => new QJoinRequestNotification
        {
            GroupId = join.GroupId,
            NotificationSeq = join.NotificationSeq,
            InitiatorId = join.InitiatorId,
            State = ToQqState(join.State),
            Comment = string.IsNullOrEmpty(join.Comment) ? null : join.Comment,
            IsFiltered = join.IsFiltered,
            OperatorId = join.OperatorId
        },
        Mk.InvitedJoinRequestGroupNotification invited => new QInvitedJoinRequestNotification
        {
            GroupId = invited.GroupId,
            NotificationSeq = invited.NotificationSeq,
            InitiatorId = invited.InitiatorId,
            TargetUserId = invited.TargetUserId,
            State = invited.State switch
            {
                Mk.InvitedJoinRequestGroupNotificationState.Accepted => QRequestState.Accepted,
                Mk.InvitedJoinRequestGroupNotificationState.Rejected => QRequestState.Rejected,
                Mk.InvitedJoinRequestGroupNotificationState.Ignored => QRequestState.Ignored,
                _ => QRequestState.Pending
            },
            OperatorId = invited.OperatorId
        },
        Mk.AdminChangeGroupNotification admin => new QAdminChangeNotification
        {
            GroupId = admin.GroupId,
            NotificationSeq = admin.NotificationSeq,
            TargetUserId = admin.TargetUserId,
            IsSet = admin.IsSet,
            OperatorId = admin.OperatorId
        },
        Mk.KickGroupNotification kick => new QKickNotification
        {
            GroupId = kick.GroupId,
            NotificationSeq = kick.NotificationSeq,
            TargetUserId = kick.TargetUserId,
            OperatorId = kick.OperatorId
        },
        Mk.QuitGroupNotification quit => new QQuitNotification
        {
            GroupId = quit.GroupId,
            NotificationSeq = quit.NotificationSeq,
            TargetUserId = quit.TargetUserId
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
public sealed class QFileApi : IQFileApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<string> UploadPrivateFileAsync(long userId, string fileUri, string fileName)
    {
        var response = await Milky.RequestAsync<UploadPrivateFileRequest, UploadPrivateFileResponse>(
            new UploadPrivateFileRequest(userId, ResourceUriConverter.Convert(fileUri), fileName));
        return response.FileId;
    }

    public async Task<string> UploadGroupFileAsync(long groupId, string fileUri, string fileName, string parentFolderId = "/")
    {
        var response = await Milky.RequestAsync<UploadGroupFileRequest, UploadGroupFileResponse>(
            new UploadGroupFileRequest(groupId, ResourceUriConverter.Convert(fileUri), fileName, parentFolderId));
        return response.FileId;
    }

    public async Task<string> GetPrivateFileDownloadUrlAsync(long userId, string fileId, string fileHash)
        => await GetPrivateFileDownloadUrlAsync(userId, fileId, fileHash, false);

    public async Task<string> GetPrivateFileDownloadUrlAsync(
        long userId, string fileId, string fileHash, bool isSelfSend)
    {
        var response = await Milky.RequestAsync<GetPrivateFileDownloadUrlRequest, GetPrivateFileDownloadUrlResponse>(
            new GetPrivateFileDownloadUrlRequest(userId, fileId, fileHash, isSelfSend));
        return response.DownloadUrl;
    }

    public async Task<string> GetGroupFileDownloadUrlAsync(long groupId, string fileId)
    {
        var response = await Milky.RequestAsync<GetGroupFileDownloadUrlRequest, GetGroupFileDownloadUrlResponse>(
            new GetGroupFileDownloadUrlRequest(groupId, fileId));
        return response.DownloadUrl;
    }

    public async Task<(IReadOnlyList<QGroupFile> Files, IReadOnlyList<QGroupFolder> Folders)> GetGroupFilesAsync(
        long groupId, string parentFolderId = "/")
    {
        var response = await Milky.RequestAsync<GetGroupFilesRequest, GetGroupFilesResponse>(
            new GetGroupFilesRequest(groupId, parentFolderId));

        var files = response.Files
            .Select(file => new QGroupFile
            {
                GroupId = file.GroupId,
                FileId = file.FileId,
                FileName = file.FileName,
                ParentFolderId = file.ParentFolderId,
                FileSize = file.FileSize,
                UploadedTime = DateTimeOffset.FromUnixTimeSeconds(file.UploadedTime),
                UploaderId = file.UploaderId,
                DownloadedTimes = file.DownloadedTimes,
                ExpireTime = file.ExpireTime is { } expire and > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(expire)
                    : null
            })
            .ToArray();

        var folders = response.Folders
            .Select(folder => new QGroupFolder
            {
                GroupId = folder.GroupId,
                FolderId = folder.FolderId,
                ParentFolderId = folder.ParentFolderId,
                FolderName = folder.FolderName,
                CreatedTime = DateTimeOffset.FromUnixTimeSeconds(folder.CreatedTime),
                LastModifiedTime = DateTimeOffset.FromUnixTimeSeconds(folder.LastModifiedTime),
                CreatorId = folder.CreatorId,
                FileCount = folder.FileCount
            })
            .ToArray();

        return (files, folders);
    }

    public Task MoveGroupFileAsync(long groupId, string fileId, string targetFolderId, string parentFolderId = "/") =>
        Milky.RequestAsync(new MoveGroupFileRequest(groupId, fileId, targetFolderId, parentFolderId));

    public Task RenameGroupFileAsync(long groupId, string fileId, string newFileName, string parentFolderId = "/") =>
        Milky.RequestAsync(new RenameGroupFileRequest(groupId, fileId, newFileName, parentFolderId));

    public Task DeleteGroupFileAsync(long groupId, string fileId) =>
        Milky.RequestAsync(new DeleteGroupFileRequest(groupId, fileId));

    public async Task<string> CreateGroupFolderAsync(long groupId, string folderName)
    {
        var response = await Milky.RequestAsync<CreateGroupFolderRequest, CreateGroupFolderResponse>(
            new CreateGroupFolderRequest(groupId, folderName));
        return response.FolderId;
    }

    public Task RenameGroupFolderAsync(long groupId, string folderId, string newFolderName) =>
        Milky.RequestAsync(new RenameGroupFolderRequest(groupId, folderId, newFolderName));

    public Task DeleteGroupFolderAsync(long groupId, string folderId) =>
        Milky.RequestAsync(new DeleteGroupFolderRequest(groupId, folderId));

    public Task PersistGroupFileAsync(long groupId, string fileId) =>
        Milky.RequestAsync(new PersistGroupFileRequest(groupId, fileId));
}

/// <summary>IQSystemApi 的 Milky 实现。</summary>
public sealed class QSystemApi : IQSystemApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<QUserProfile> GetUserProfileAsync(long userId)
    {
        var response = await Milky.RequestAsync<GetUserProfileRequest, GetUserProfileResponse>(
            new GetUserProfileRequest(userId));
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

    public async Task<IReadOnlyList<QFriend>> GetFriendListAsync(bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetFriendListRequest, GetFriendListResponse>(
            new GetFriendListRequest(noCache));
        return response.Friends.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QFriend> GetFriendInfoAsync(long userId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetFriendInfoRequest, GetFriendInfoResponse>(
            new GetFriendInfoRequest(userId, noCache));
        return QModelMapper.ToQq(response.Friend);
    }

    public async Task<IReadOnlyList<QGroup>> GetGroupListAsync(bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupListRequest, GetGroupListResponse>(
            new GetGroupListRequest(noCache));
        return response.Groups.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QGroup> GetGroupInfoAsync(long groupId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupInfoRequest, GetGroupInfoResponse>(
            new GetGroupInfoRequest(groupId, noCache));
        return QModelMapper.ToQq(response.Group);
    }

    public async Task<IReadOnlyList<QGroupMember>> GetGroupMemberListAsync(long groupId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupMemberListRequest, GetGroupMemberListResponse>(
            new GetGroupMemberListRequest(groupId, noCache));
        return response.Members.Select(QModelMapper.ToQq).ToArray();
    }

    public async Task<QGroupMember> GetGroupMemberInfoAsync(long groupId, long userId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupMemberInfoRequest, GetGroupMemberInfoResponse>(
            new GetGroupMemberInfoRequest(groupId, userId, noCache));
        return QModelMapper.ToQq(response.Member);
    }

    public async Task<(IReadOnlyList<QFriend> Friends, IReadOnlyList<QGroup> Groups)> GetPeerPinsAsync()
    {
        var response = await Milky.RequestAsync<GetPeerPinsRequest, GetPeerPinsResponse>(new GetPeerPinsRequest());
        return (
            response.Friends.Select(QModelMapper.ToQq).ToArray(),
            response.Groups.Select(QModelMapper.ToQq).ToArray());
    }

    public Task SetAvatarAsync(string imageUri) =>
        Milky.RequestAsync(new SetAvatarRequest(ResourceUriConverter.Convert(imageUri)));

    public Task SetNicknameAsync(string nickname) =>
        Milky.RequestAsync(new SetNicknameRequest(nickname));

    public Task SetBioAsync(string bio) =>
        Milky.RequestAsync(new SetBioRequest(bio));

    public async Task<string> GetCookiesAsync(string domain)
    {
        var response = await Milky.RequestAsync<GetCookiesRequest, GetCookiesResponse>(new GetCookiesRequest(domain));
        return response.Cookies;
    }

    public async Task<string> GetCsrfTokenAsync()
    {
        var response = await Milky.RequestAsync<GetCsrfTokenRequest, GetCsrfTokenResponse>(new GetCsrfTokenRequest());
        return response.CsrfToken;
    }

    async Task<QLoginInfo> IQSystemApi.GetLoginInfoAsync()
    {
        var response = await Milky.RequestAsync<GetLoginInfoRequest, GetLoginInfoResponse>(new GetLoginInfoRequest());
        return new QLoginInfo(response.Uin, response.Nickname);
    }

    async Task<QImplInfo> IQSystemApi.GetImplInfoAsync()
    {
        var response = await Milky.RequestAsync<GetImplInfoRequest, GetImplInfoResponse>(new GetImplInfoRequest());
        return new QImplInfo
        {
            ImplName = response.ImplName,
            ImplVersion = response.ImplVersion,
            QqProtocolVersion = response.QqProtocolVersion,
            QqProtocolType = response.QqProtocolType.ToString().ToLowerInvariant(),
            ProtocolVersion = response.MilkyVersion
        };
    }

    async Task<IReadOnlyList<string>> IQSystemApi.GetCustomFaceUrlListAsync()
    {
        var response = await Milky.RequestAsync<GetCustomFaceUrlListRequest, GetCustomFaceUrlListResponse>(
            new GetCustomFaceUrlListRequest());
        return response.Urls;
    }

    public Task SetPeerPinAsync(QMessageScene scene, long peerId, bool isPinned = true) =>
        Milky.RequestAsync(new SetPeerPinRequest(
            scene switch
            {
                QMessageScene.Group => SetPeerPinRequestMessageScene.Group,
                QMessageScene.Temp => SetPeerPinRequestMessageScene.Temp,
                _ => SetPeerPinRequestMessageScene.Friend
            },
            peerId,
            isPinned));
}

/// <summary>IQMessageApi 的 Milky 实现。</summary>
public sealed class QMessageApi : IQMessageApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<long> SendMessageAsync(
        QMessageScene scene,
        long peerId,
        IReadOnlyList<QOutgoingSegment> segments)
        => (await SendMessageDetailedAsync(scene, peerId, segments)).MessageSeq;

    public async Task<QSentMessage> SendMessageDetailedAsync(
        QMessageScene scene,
        long peerId,
        IReadOnlyList<QOutgoingSegment> segments)
    {
        var milkySegments = ResourceUriConverter.Convert(segments.Select(QModelMapper.ToMilky).ToArray());

        if (scene == QMessageScene.Group)
        {
            var response = await Milky.RequestAsync<SendGroupMessageRequest, SendGroupMessageResponse>(
                new SendGroupMessageRequest(peerId, milkySegments));
            return new QSentMessage(response.MessageSeq, DateTimeOffset.FromUnixTimeSeconds(response.Time));
        }

        var privateResponse = await Milky.RequestAsync<SendPrivateMessageRequest, SendPrivateMessageResponse>(
            new SendPrivateMessageRequest(peerId, milkySegments));
        return new QSentMessage(
            privateResponse.MessageSeq,
            DateTimeOffset.FromUnixTimeSeconds(privateResponse.Time));
    }

    public async Task<QIncomingMessage?> GetMessageAsync(QMessageScene scene, long peerId, long messageSeq)
    {
        var response = await Milky.RequestAsync<GetMessageRequest, GetMessageResponse>(new GetMessageRequest(
            scene switch
            {
                QMessageScene.Group => GetMessageRequestMessageScene.Group,
                QMessageScene.Temp => GetMessageRequestMessageScene.Temp,
                _ => GetMessageRequestMessageScene.Friend
            },
            peerId,
            messageSeq));
        return QModelMapper.ToQq(response.Message);
    }

    public async Task<(IReadOnlyList<QIncomingMessage> Messages, long? NextMessageSeq)> GetHistoryMessagesAsync(
        QMessageScene scene, long peerId, long? startMessageSeq = null, int limit = 20)
    {
        var response = await Milky.RequestAsync<GetHistoryMessagesRequest, GetHistoryMessagesResponse>(
            new GetHistoryMessagesRequest(
                scene switch
                {
                    QMessageScene.Group => GetHistoryMessagesRequestMessageScene.Group,
                    QMessageScene.Temp => GetHistoryMessagesRequestMessageScene.Temp,
                    _ => GetHistoryMessagesRequestMessageScene.Friend
                },
                peerId,
                startMessageSeq,
                limit));

        var messages = response.Messages
            .Select(QModelMapper.ToQq)
            .Where(message => message is not null)
            .Cast<QIncomingMessage>()
            .ToArray();

        return (messages, response.NextMessageSeq);
    }

    public Task RecallMessageAsync(QMessageScene scene, long peerId, long messageSeq) =>
        scene == QMessageScene.Group
            ? Milky.RequestAsync(new RecallGroupMessageRequest(peerId, messageSeq))
            : Milky.RequestAsync(new RecallPrivateMessageRequest(peerId, messageSeq));

    public async Task<string> GetResourceTempUrlAsync(string resourceId)
    {
        var response = await Milky.RequestAsync<GetResourceTempUrlRequest, GetResourceTempUrlResponse>(
            new GetResourceTempUrlRequest(resourceId));
        return response.Url;
    }

    public async Task<IReadOnlyList<QForwardedIncomingMessage>> GetForwardedMessagesAsync(string forwardId)
    {
        var response = await Milky.RequestAsync<GetForwardedMessagesRequest, GetForwardedMessagesResponse>(
            new GetForwardedMessagesRequest(forwardId));
        return response.Messages
            .Select(message => new QForwardedIncomingMessage
            {
                MessageSeq = message.MessageSeq,
                SenderName = message.SenderName,
                AvatarUrl = message.AvatarUrl,
                Time = DateTimeOffset.FromUnixTimeSeconds(message.Time),
                Segments = QModelMapper.ToQq(message.Segments)
            })
            .ToArray();
    }

    public Task MarkAsReadAsync(QMessageScene scene, long peerId, long messageSeq) =>
        Milky.RequestAsync(new MarkMessageAsReadRequest(
            scene switch
            {
                QMessageScene.Group => MarkMessageAsReadRequestMessageScene.Group,
                QMessageScene.Temp => MarkMessageAsReadRequestMessageScene.Temp,
                _ => MarkMessageAsReadRequestMessageScene.Friend
            },
            peerId,
            messageSeq));
}
