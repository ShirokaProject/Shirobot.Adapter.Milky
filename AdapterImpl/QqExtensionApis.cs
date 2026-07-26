using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.File.Requests;
using ShiroBot.Model.File.Responses;
using ShiroBot.Model.Friend.Requests;
using ShiroBot.Model.Group.Requests;
using ShiroBot.Model.Group.Responses;
using ShiroBot.Model.Message.Requests;
using ShiroBot.Model.Message.Responses;
using ShiroBot.Model.System.Requests;
using ShiroBot.Model.System.Responses;
using ShiroBot.Qq.Model;
using Mk = ShiroBot.Model.Common;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

/// <summary>IQqFriendApi 的 Milky 实现。</summary>
public sealed class QqFriendApi : IQqFriendApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public Task SendNudgeAsync(long userId, bool isSelf = false) =>
        Milky.RequestAsync(new SendFriendNudgeRequest(userId, isSelf));

    public Task SendProfileLikeAsync(long userId, int count = 1) =>
        Milky.RequestAsync(new SendProfileLikeRequest(userId, count));

    public Task DeleteFriendAsync(long userId) =>
        Milky.RequestAsync(new DeleteFriendRequest(userId));
}

/// <summary>IQqGroupApi 的 Milky 实现。</summary>
public sealed class QqGroupApi : IQqGroupApi
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

    public async Task<IReadOnlyList<QqGroupAnnouncement>> GetAnnouncementsAsync(long groupId)
    {
        var response = await Milky.RequestAsync<GetGroupAnnouncementsRequest, GetGroupAnnouncementsResponse>(
            new GetGroupAnnouncementsRequest(groupId));
        return response.Announcements
            .Select(announcement => new QqGroupAnnouncement
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

    public async Task<IReadOnlyList<QqEssenceMessage>> GetEssenceMessagesAsync(long groupId, int pageIndex, int pageSize)
    {
        var response = await Milky.RequestAsync<GetGroupEssenceMessagesRequest, GetGroupEssenceMessagesResponse>(
            new GetGroupEssenceMessagesRequest(groupId, pageIndex, pageSize));
        return response.Messages
            .Select(message => new QqEssenceMessage
            {
                GroupId = message.GroupId,
                MessageSeq = message.MessageSeq,
                MessageTime = DateTimeOffset.FromUnixTimeSeconds(message.MessageTime),
                SenderId = message.SenderId,
                SenderName = message.SenderName,
                OperatorId = message.OperatorId,
                OperatorName = message.OperatorName,
                OperationTime = DateTimeOffset.FromUnixTimeSeconds(message.OperationTime),
                Segments = QqModelMapper.ToQq(message.Segments)
            })
            .ToArray();
    }

    public Task SetEssenceMessageAsync(long groupId, long messageSeq, bool isSet = true) =>
        Milky.RequestAsync(new SetGroupEssenceMessageRequest(groupId, messageSeq, isSet));

    public Task AcceptJoinRequestAsync(Qq.Model.QqGroupJoinRequest request) =>
        Milky.RequestAsync(new AcceptGroupRequestRequest(
            request.NotificationSeq,
            AcceptGroupRequestRequestNotificationType.JoinRequest,
            request.GroupId,
            request.IsFiltered));

    public Task RejectJoinRequestAsync(Qq.Model.QqGroupJoinRequest request, string? reason = null) =>
        Milky.RequestAsync(new RejectGroupRequestRequest(
            request.NotificationSeq,
            RejectGroupRequestRequestNotificationType.JoinRequest,
            request.GroupId,
            request.IsFiltered,
            reason));
}

/// <summary>IQqFileApi 的 Milky 实现。</summary>
public sealed class QqFileApi : IQqFileApi
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
    {
        var response = await Milky.RequestAsync<GetPrivateFileDownloadUrlRequest, GetPrivateFileDownloadUrlResponse>(
            new GetPrivateFileDownloadUrlRequest(userId, fileId, fileHash));
        return response.DownloadUrl;
    }

    public async Task<string> GetGroupFileDownloadUrlAsync(long groupId, string fileId)
    {
        var response = await Milky.RequestAsync<GetGroupFileDownloadUrlRequest, GetGroupFileDownloadUrlResponse>(
            new GetGroupFileDownloadUrlRequest(groupId, fileId));
        return response.DownloadUrl;
    }

    public async Task<(IReadOnlyList<QqGroupFile> Files, IReadOnlyList<QqGroupFolder> Folders)> GetGroupFilesAsync(
        long groupId, string parentFolderId = "/")
    {
        var response = await Milky.RequestAsync<GetGroupFilesRequest, GetGroupFilesResponse>(
            new GetGroupFilesRequest(groupId, parentFolderId));

        var files = response.Files
            .Select(file => new QqGroupFile
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
            .Select(folder => new QqGroupFolder
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
}

/// <summary>IQqSystemApi 的 Milky 实现。</summary>
public sealed class QqSystemApi : IQqSystemApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<QqUserProfile> GetUserProfileAsync(long userId)
    {
        var response = await Milky.RequestAsync<GetUserProfileRequest, GetUserProfileResponse>(
            new GetUserProfileRequest(userId));
        return new QqUserProfile
        {
            UserId = userId,
            Nickname = response.Nickname,
            Qid = string.IsNullOrEmpty(response.Qid) ? null : response.Qid,
            Age = response.Age,
            Sex = response.Sex switch
            {
                GetUserProfileResponseSex.Male => QqSex.Male,
                GetUserProfileResponseSex.Female => QqSex.Female,
                _ => QqSex.Unknown
            },
            Remark = string.IsNullOrEmpty(response.Remark) ? null : response.Remark,
            Bio = string.IsNullOrEmpty(response.Bio) ? null : response.Bio,
            Level = response.Level,
            Country = string.IsNullOrEmpty(response.Country) ? null : response.Country,
            City = string.IsNullOrEmpty(response.City) ? null : response.City,
            School = string.IsNullOrEmpty(response.School) ? null : response.School
        };
    }

    public async Task<IReadOnlyList<QqFriend>> GetFriendListAsync(bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetFriendListRequest, GetFriendListResponse>(
            new GetFriendListRequest(noCache));
        return response.Friends.Select(QqModelMapper.ToQq).ToArray();
    }

    public async Task<QqFriend> GetFriendInfoAsync(long userId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetFriendInfoRequest, GetFriendInfoResponse>(
            new GetFriendInfoRequest(userId, noCache));
        return QqModelMapper.ToQq(response.Friend);
    }

    public async Task<IReadOnlyList<QqGroup>> GetGroupListAsync(bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupListRequest, GetGroupListResponse>(
            new GetGroupListRequest(noCache));
        return response.Groups.Select(QqModelMapper.ToQq).ToArray();
    }

    public async Task<QqGroup> GetGroupInfoAsync(long groupId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupInfoRequest, GetGroupInfoResponse>(
            new GetGroupInfoRequest(groupId, noCache));
        return QqModelMapper.ToQq(response.Group);
    }

    public async Task<IReadOnlyList<QqGroupMember>> GetGroupMemberListAsync(long groupId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupMemberListRequest, GetGroupMemberListResponse>(
            new GetGroupMemberListRequest(groupId, noCache));
        return response.Members.Select(QqModelMapper.ToQq).ToArray();
    }

    public async Task<QqGroupMember> GetGroupMemberInfoAsync(long groupId, long userId, bool noCache = false)
    {
        var response = await Milky.RequestAsync<GetGroupMemberInfoRequest, GetGroupMemberInfoResponse>(
            new GetGroupMemberInfoRequest(groupId, userId, noCache));
        return QqModelMapper.ToQq(response.Member);
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
}

/// <summary>IQqMessageApi 的 Milky 实现。</summary>
public sealed class QqMessageApi : IQqMessageApi
{
    private static MilkyClient Milky => MilkyClientManager.Instance;

    public async Task<long> SendMessageAsync(
        QqMessageScene scene,
        long peerId,
        IReadOnlyList<QqOutgoingSegment> segments)
    {
        var milkySegments = ResourceUriConverter.Convert(segments.Select(QqModelMapper.ToMilky).ToArray());

        if (scene == QqMessageScene.Group)
        {
            var response = await Milky.RequestAsync<SendGroupMessageRequest, SendGroupMessageResponse>(
                new SendGroupMessageRequest(peerId, milkySegments));
            return response.MessageSeq;
        }

        var privateResponse = await Milky.RequestAsync<SendPrivateMessageRequest, SendPrivateMessageResponse>(
            new SendPrivateMessageRequest(peerId, milkySegments));
        return privateResponse.MessageSeq;
    }

    public async Task<IReadOnlyList<QqForwardedIncomingMessage>> GetForwardedMessagesAsync(string forwardId)
    {
        var response = await Milky.RequestAsync<GetForwardedMessagesRequest, GetForwardedMessagesResponse>(
            new GetForwardedMessagesRequest(forwardId));
        return response.Messages
            .Select(message => new QqForwardedIncomingMessage
            {
                SenderName = message.SenderName,
                AvatarUrl = message.AvatarUrl,
                Time = DateTimeOffset.FromUnixTimeSeconds(message.Time),
                Segments = QqModelMapper.ToQq(message.Segments)
            })
            .ToArray();
    }

    public Task MarkAsReadAsync(QqMessageScene scene, long peerId, long messageSeq) =>
        Milky.RequestAsync(new MarkMessageAsReadRequest(
            scene switch
            {
                QqMessageScene.Group => MarkMessageAsReadRequestMessageScene.Group,
                QqMessageScene.Temp => MarkMessageAsReadRequestMessageScene.Temp,
                _ => MarkMessageAsReadRequestMessageScene.Friend
            },
            peerId,
            messageSeq));
}
