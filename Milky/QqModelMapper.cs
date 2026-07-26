using ShiroBot.Qq.Model;
using Mk = ShiroBot.Model.Common;

namespace ShiroBot.MilkyAdapter.Milky;

/// <summary>
/// Milky 协议模型 → ShiroBot 通用 QQ 模型(ShiroBot.Qq.Model)的转换。
/// 通用 QQ 模型是插件面向的稳定契约;Milky 只是其中一种承载协议。
/// </summary>
internal static class QqModelMapper
{
    // ─── 实体 ───

    public static QqFriend ToQq(Mk.FriendEntity friend) => new()
    {
        UserId = friend.UserId,
        Nickname = friend.Nickname,
        Sex = ToQq(friend.Sex),
        Qid = string.IsNullOrEmpty(friend.Qid) ? null : friend.Qid,
        Remark = string.IsNullOrEmpty(friend.Remark) ? null : friend.Remark,
        Category = new QqFriendCategory(friend.Category.CategoryId, friend.Category.CategoryName)
    };

    public static QqGroup ToQq(Mk.GroupEntity group) => new()
    {
        GroupId = group.GroupId,
        GroupName = group.GroupName,
        MemberCount = group.MemberCount,
        MaxMemberCount = group.MaxMemberCount,
        Remark = string.IsNullOrEmpty(group.Remark) ? null : group.Remark,
        CreatedTime = group.CreatedTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(group.CreatedTime),
        Description = string.IsNullOrEmpty(group.Description) ? null : group.Description,
        Announcement = string.IsNullOrEmpty(group.Announcement) ? null : group.Announcement
    };

    public static QqGroupMember ToQq(Mk.GroupMemberEntity member) => new()
    {
        UserId = member.UserId,
        Nickname = member.Nickname,
        GroupId = member.GroupId,
        Sex = member.Sex switch
        {
            Mk.GroupMemberEntitySex.Male => QqSex.Male,
            Mk.GroupMemberEntitySex.Female => QqSex.Female,
            _ => QqSex.Unknown
        },
        Card = string.IsNullOrEmpty(member.Card) ? null : member.Card,
        Title = string.IsNullOrEmpty(member.Title) ? null : member.Title,
        Level = member.Level,
        Role = member.Role switch
        {
            Mk.GroupMemberEntityRole.Owner => QqGroupRole.Owner,
            Mk.GroupMemberEntityRole.Admin => QqGroupRole.Admin,
            _ => QqGroupRole.Member
        },
        JoinTime = member.JoinTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(member.JoinTime),
        LastSentTime = member.LastSentTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(member.LastSentTime),
        ShutUpEndTime = member.ShutUpEndTime is { } shutUp and > 0
            ? DateTimeOffset.FromUnixTimeSeconds(shutUp)
            : null
    };

    private static QqSex ToQq(Mk.FriendEntitySex sex) => sex switch
    {
        Mk.FriendEntitySex.Male => QqSex.Male,
        Mk.FriendEntitySex.Female => QqSex.Female,
        _ => QqSex.Unknown
    };

    // ─── 入站消息 ───

    public static QqIncomingMessage? ToQq(Mk.IncomingMessage message) => message switch
    {
        Mk.FriendIncomingMessage friend => new QqFriendMessage
        {
            PeerId = friend.PeerId,
            MessageSeq = friend.MessageSeq,
            SenderId = friend.SenderId,
            Time = DateTimeOffset.FromUnixTimeSeconds(friend.Time),
            Segments = ToQq(friend.Segments),
            Friend = ToQq(friend.Friend)
        },
        Mk.GroupIncomingMessage group => new QqGroupMessage
        {
            PeerId = group.PeerId,
            MessageSeq = group.MessageSeq,
            SenderId = group.SenderId,
            Time = DateTimeOffset.FromUnixTimeSeconds(group.Time),
            Segments = ToQq(group.Segments),
            Group = ToQq(group.Group),
            GroupMember = ToQq(group.GroupMember)
        },
        Mk.TempIncomingMessage temp => new QqTempMessage
        {
            PeerId = temp.PeerId,
            MessageSeq = temp.MessageSeq,
            SenderId = temp.SenderId,
            Time = DateTimeOffset.FromUnixTimeSeconds(temp.Time),
            Segments = ToQq(temp.Segments),
            Group = temp.Group is null ? null : ToQq(temp.Group)
        },
        _ => null
    };

    // ─── 段 ───

    public static IReadOnlyList<QqIncomingSegment> ToQq(IReadOnlyList<Mk.IncomingSegment> segments) =>
        segments.Select(ToQq).ToArray();

    public static QqIncomingSegment ToQq(Mk.IncomingSegment segment) => segment switch
    {
        Mk.TextIncomingSegment text => new QqTextIncoming(text.Text),
        Mk.MentionIncomingSegment mention => new QqMentionIncoming(mention.UserId, mention.Name),
        Mk.MentionAllIncomingSegment => new QqMentionAllIncoming(),
        Mk.FaceIncomingSegment face => new QqFaceIncoming(face.FaceId, face.IsLarge),
        Mk.ReplyIncomingSegment reply => new QqReplyIncoming(reply.MessageSeq)
        {
            SenderId = reply.SenderId,
            SenderName = reply.SenderName,
            Time = DateTimeOffset.FromUnixTimeSeconds(reply.Time),
            Segments = ToQq(reply.Segments)
        },
        Mk.ImageIncomingSegment image => new QqImageIncoming(image.ResourceId, image.TempUrl)
        {
            Width = image.Width,
            Height = image.Height,
            Summary = string.IsNullOrEmpty(image.Summary) ? null : image.Summary,
            SubType = image.SubType.ToString().ToLowerInvariant()
        },
        Mk.RecordIncomingSegment record => new QqRecordIncoming(record.ResourceId, record.TempUrl)
        {
            Duration = TimeSpan.FromSeconds(record.Duration)
        },
        Mk.VideoIncomingSegment video => new QqVideoIncoming(video.ResourceId, video.TempUrl)
        {
            Width = video.Width,
            Height = video.Height,
            Duration = TimeSpan.FromSeconds(video.Duration)
        },
        Mk.FileIncomingSegment file => new QqFileIncoming(file.FileId, file.FileName, file.FileSize)
        {
            FileHash = file.FileHash
        },
        Mk.ForwardIncomingSegment forward => new QqForwardIncoming(forward.ForwardId)
        {
            Title = forward.Title,
            Preview = forward.Preview,
            Summary = forward.Summary
        },
        Mk.MarketFaceIncomingSegment face => new QqMarketFaceIncoming(face.EmojiId, face.Url)
        {
            EmojiPackageId = face.EmojiPackageId,
            Key = face.Key,
            Summary = face.Summary
        },
        Mk.LightAppIncomingSegment app => new QqLightAppIncoming(app.AppName, app.JsonPayload),
        Mk.XmlIncomingSegment xml => new QqXmlIncoming(xml.ServiceId, xml.XmlPayload),
        Mk.MarkdownIncomingSegment markdown => new QqMarkdownIncoming(markdown.Content),
        _ => new QqTextIncoming(string.Empty)
    };

    public static Mk.OutgoingSegment ToMilky(QqOutgoingSegment segment) => segment switch
    {
        QqTextOutgoing text => new Mk.TextOutgoingSegment(text.Text),
        QqMentionOutgoing mention => new Mk.MentionOutgoingSegment(mention.UserId),
        QqMentionAllOutgoing => new Mk.MentionAllOutgoingSegment(),
        QqFaceOutgoing face => new Mk.FaceOutgoingSegment(face.FaceId, face.IsLarge),
        QqReplyOutgoing reply => new Mk.ReplyOutgoingSegment(reply.MessageSeq),
        QqImageOutgoing image => new Mk.ImageOutgoingSegment(
            image.Uri,
            string.Equals(image.SubType, "sticker", StringComparison.OrdinalIgnoreCase)
                ? Mk.ImageOutgoingSegmentSubType.Sticker
                : Mk.ImageOutgoingSegmentSubType.Normal,
            image.Summary),
        QqRecordOutgoing record => new Mk.RecordOutgoingSegment(record.Uri),
        QqLightAppOutgoing lightApp => new Mk.LightAppOutgoingSegment(lightApp.JsonPayload),
        QqVideoOutgoing video => new Mk.VideoOutgoingSegment(video.Uri, video.ThumbUri),
        QqForwardOutgoing forward => new Mk.ForwardOutgoingSegment(
            forward.Messages
                .Select(message => new Mk.OutgoingForwardedMessage(
                    message.UserId,
                    message.SenderName,
                    message.Segments.Select(ToMilky).ToArray(),
                    message.Time?.ToUnixTimeSeconds()))
                .ToArray(),
            forward.Title,
            forward.Preview,
            forward.Summary,
            forward.Prompt),
        _ => throw new NotSupportedException($"未知的 QQ 出站段: {segment.GetType().Name}")
    };

    // ─── 事件负载 ───

    /// <summary>把 Milky 特有事件转为通用 QQ 事件负载;非平台特有事件返回 null。</summary>
    public static QqEventPayload? ToQqEventPayload(Mk.Event e) => e switch
    {
        Mk.FriendNudgeEvent x => new QqFriendNudge
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            UserId = x.UserId,
            IsSelfSend = x.IsSelfSend,
            IsSelfReceive = x.IsSelfReceive,
            DisplayAction = x.DisplayAction,
            DisplaySuffix = x.DisplaySuffix
        },
        Mk.FriendFileUploadEvent x => new QqFriendFileUpload
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            UserId = x.UserId,
            FileId = x.FileId,
            FileName = x.FileName,
            FileSize = x.FileSize,
            FileHash = x.FileHash,
            IsSelf = x.IsSelf
        },
        Mk.GroupAdminChangeEvent x => new QqGroupAdminChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId,
            IsSet = x.IsSet
        },
        Mk.GroupEssenceMessageChangeEvent x => new QqGroupEssenceMessageChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            MessageSeq = x.MessageSeq,
            OperatorId = x.OperatorId,
            IsSet = x.IsSet
        },
        Mk.GroupNameChangeEvent x => new QqGroupNameChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NewGroupName = x.NewGroupName,
            OperatorId = x.OperatorId
        },
        Mk.GroupMessageReactionEvent x => new QqGroupMessageReaction
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            MessageSeq = x.MessageSeq,
            FaceId = x.FaceId,
            IsAdd = x.IsAdd
        },
        Mk.GroupMuteEvent x => new QqGroupMute
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId,
            Duration = TimeSpan.FromSeconds(x.Duration)
        },
        Mk.GroupWholeMuteEvent x => new QqGroupWholeMute
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            OperatorId = x.OperatorId,
            IsMute = x.IsMute
        },
        Mk.GroupNudgeEvent x => new QqGroupNudge
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            SenderId = x.SenderId,
            ReceiverId = x.ReceiverId,
            DisplayAction = x.DisplayAction,
            DisplaySuffix = x.DisplaySuffix
        },
        Mk.GroupFileUploadEvent x => new QqGroupFileUpload
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            FileId = x.FileId,
            FileName = x.FileName,
            FileSize = x.FileSize
        },
        Mk.GroupJoinRequestEvent x => new Qq.Model.QqGroupJoinRequest
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NotificationSeq = x.NotificationSeq,
            InitiatorId = x.InitiatorId,
            Comment = x.Comment,
            IsFiltered = x.IsFiltered
        },
        Mk.GroupInvitedJoinRequestEvent x => new QqGroupInvitedJoinRequest
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NotificationSeq = x.NotificationSeq,
            InitiatorId = x.InitiatorId,
            TargetUserId = x.TargetUserId
        },
        Mk.GroupDisbandEvent x => new QqGroupDisband
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            OperatorId = x.OperatorId
        },
        Mk.PeerPinChangeEvent x => new QqPeerPinChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            Scene = x.MessageScene switch
            {
                Mk.PeerPinChangeEventMessageScene.Group => QqMessageScene.Group,
                Mk.PeerPinChangeEventMessageScene.Temp => QqMessageScene.Temp,
                _ => QqMessageScene.Friend
            },
            PeerId = x.PeerId,
            IsPinned = x.IsPinned
        },
        _ => null
    };
}
