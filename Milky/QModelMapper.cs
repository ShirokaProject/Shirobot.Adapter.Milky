using ShiroBot.Model.QQ;
using Mk = ShiroBot.Adapter.Milky.Model.Common;

namespace ShiroBot.Adapter.Milky.Milky;

/// <summary>
/// Converts Milky protocol models to the shared QQ contract (ShiroBot.Model.QQ).
/// 通用 QQ 模型是插件面向的稳定契约;Milky 只是其中一种承载协议。
/// </summary>
internal static class QModelMapper
{
    // ─── 实体 ───

    public static QFriend ToQq(Mk.FriendEntity friend) => new()
    {
        UserId = friend.UserId,
        Nickname = friend.Nickname,
        Sex = ToQq(friend.Sex),
        Qid = string.IsNullOrEmpty(friend.Qid) ? null : friend.Qid,
        Remark = string.IsNullOrEmpty(friend.Remark) ? null : friend.Remark,
        Category = new QFriendCategory(friend.Category.CategoryId, friend.Category.CategoryName)
    };

    public static QGroup ToQq(Mk.GroupEntity group) => new()
    {
        GroupId = group.GroupId,
        GroupName = group.GroupName,
        MemberCount = group.MemberCount,
        MaxMemberCount = group.MaxMemberCount,
        Remark = string.IsNullOrEmpty(group.Remark) ? null : group.Remark,
        CreatedTime = group.CreatedTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(group.CreatedTime),
        Description = string.IsNullOrEmpty(group.Description) ? null : group.Description,
        Announcement = string.IsNullOrEmpty(group.Announcement) ? null : group.Announcement,
        Question = string.IsNullOrEmpty(group.Question) ? null : group.Question
    };

    public static QGroupMember ToQq(Mk.GroupMemberEntity member) => new()
    {
        UserId = member.UserId,
        Nickname = member.Nickname,
        GroupId = member.GroupId,
        Sex = member.Sex switch
        {
            Mk.GroupMemberEntitySex.Male => QSex.Male,
            Mk.GroupMemberEntitySex.Female => QSex.Female,
            _ => QSex.Unknown
        },
        Card = string.IsNullOrEmpty(member.Card) ? null : member.Card,
        Title = string.IsNullOrEmpty(member.Title) ? null : member.Title,
        Level = member.Level,
        Role = member.Role switch
        {
            Mk.GroupMemberEntityRole.Owner => QGroupRole.Owner,
            Mk.GroupMemberEntityRole.Admin => QGroupRole.Admin,
            _ => QGroupRole.Member
        },
        JoinTime = member.JoinTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(member.JoinTime),
        LastSentTime = member.LastSentTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(member.LastSentTime),
        ShutUpEndTime = member.ShutUpEndTime is { } shutUp and > 0
            ? DateTimeOffset.FromUnixTimeSeconds(shutUp)
            : null
    };

    private static QSex ToQq(Mk.FriendEntitySex sex) => sex switch
    {
        Mk.FriendEntitySex.Male => QSex.Male,
        Mk.FriendEntitySex.Female => QSex.Female,
        _ => QSex.Unknown
    };

    // ─── 入站消息 ───

    public static QIncomingMessage? ToQq(Mk.IncomingMessage message) => message switch
    {
        Mk.FriendIncomingMessage friend => new QFriendMessage
        {
            PeerId = friend.PeerId,
            MessageSeq = friend.MessageSeq,
            SenderId = friend.SenderId,
            Time = DateTimeOffset.FromUnixTimeSeconds(friend.Time),
            Segments = ToQq(friend.Segments),
            Friend = ToQq(friend.Friend)
        },
        Mk.GroupIncomingMessage group => new QGroupMessage
        {
            PeerId = group.PeerId,
            MessageSeq = group.MessageSeq,
            SenderId = group.SenderId,
            Time = DateTimeOffset.FromUnixTimeSeconds(group.Time),
            Segments = ToQq(group.Segments),
            Group = ToQq(group.Group),
            GroupMember = ToQq(group.GroupMember)
        },
        Mk.TempIncomingMessage temp => new QTempMessage
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

    public static IReadOnlyList<QIncomingSegment> ToQq(IReadOnlyList<Mk.IncomingSegment> segments) =>
        segments.Select(ToQq).ToArray();

    public static QIncomingSegment ToQq(Mk.IncomingSegment segment) => segment switch
    {
        Mk.TextIncomingSegment text => new QIncomingText(text.Text),
        Mk.MentionIncomingSegment mention => new QIncomingMention(mention.UserId, mention.Name),
        Mk.MentionAllIncomingSegment => new QIncomingMentionAll(),
        Mk.FaceIncomingSegment face => new QIncomingFace(face.FaceId, face.IsLarge),
        Mk.ReplyIncomingSegment reply => new QIncomingReply(reply.MessageSeq)
        {
            SenderId = reply.SenderId,
            SenderName = reply.SenderName,
            Time = DateTimeOffset.FromUnixTimeSeconds(reply.Time),
            Segments = ToQq(reply.Segments)
        },
        Mk.ImageIncomingSegment image => new QIncomingImage(image.ResourceId, image.TempUrl)
        {
            Width = image.Width,
            Height = image.Height,
            Summary = string.IsNullOrEmpty(image.Summary) ? null : image.Summary,
            SubType = image.SubType.ToString().ToLowerInvariant()
        },
        Mk.RecordIncomingSegment record => new QIncomingRecord(record.ResourceId, record.TempUrl)
        {
            Duration = TimeSpan.FromSeconds(record.Duration)
        },
        Mk.VideoIncomingSegment video => new QIncomingVideo(video.ResourceId, video.TempUrl)
        {
            Width = video.Width,
            Height = video.Height,
            Duration = TimeSpan.FromSeconds(video.Duration)
        },
        Mk.FileIncomingSegment file => new QIncomingFile(file.FileId, file.FileName, file.FileSize)
        {
            FileHash = file.FileHash
        },
        Mk.ForwardIncomingSegment forward => new QIncomingForward(forward.ForwardId)
        {
            Title = forward.Title,
            Preview = forward.Preview,
            Summary = forward.Summary
        },
        Mk.MarketFaceIncomingSegment face => new QIncomingMarketFace(face.EmojiId, face.Url)
        {
            EmojiPackageId = face.EmojiPackageId,
            Key = face.Key,
            Summary = face.Summary
        },
        Mk.LightAppIncomingSegment app => new QIncomingLightApp(app.AppName, app.JsonPayload),
        Mk.XmlIncomingSegment xml => new QIncomingXml(xml.ServiceId, xml.XmlPayload),
        Mk.MarkdownIncomingSegment markdown => new QIncomingMarkdown(markdown.Content),
        _ => new QIncomingText(string.Empty)
    };

    public static Mk.OutgoingSegment ToMilky(QOutgoingSegment segment) => segment switch
    {
        QOutgoingText text => new Mk.TextOutgoingSegment(text.Text),
        QOutgoingMention mention => new Mk.MentionOutgoingSegment(mention.UserId),
        QOutgoingMentionAll => new Mk.MentionAllOutgoingSegment(),
        QOutgoingFace face => new Mk.FaceOutgoingSegment(face.FaceId, face.IsLarge),
        QOutgoingReply reply => new Mk.ReplyOutgoingSegment(reply.MessageSeq),
        QOutgoingImage image => new Mk.ImageOutgoingSegment(
            image.Uri,
            string.Equals(image.SubType, "sticker", StringComparison.OrdinalIgnoreCase)
                ? Mk.ImageOutgoingSegmentSubType.Sticker
                : Mk.ImageOutgoingSegmentSubType.Normal,
            image.Summary),
        QOutgoingRecord record => new Mk.RecordOutgoingSegment(record.Uri),
        QOutgoingLightApp lightApp => new Mk.LightAppOutgoingSegment(lightApp.JsonPayload),
        QOutgoingVideo video => new Mk.VideoOutgoingSegment(video.Uri, video.ThumbUri),
        QOutgoingForward forward => new Mk.ForwardOutgoingSegment(
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
    public static QEventPayload? ToQqEventPayload(Mk.Event e) => e switch
    {
        Mk.MessageRecallEvent x => new QMessageRecall
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            Scene = x.MessageScene switch
            {
                Mk.MessageRecallEventMessageScene.Group => QMessageScene.Group,
                Mk.MessageRecallEventMessageScene.Temp => QMessageScene.Temp,
                _ => QMessageScene.Friend
            },
            PeerId = x.PeerId,
            MessageSeq = x.MessageSeq,
            SenderId = x.SenderId,
            OperatorId = x.OperatorId,
            DisplaySuffix = x.DisplaySuffix
        },
        Mk.GroupMemberIncreaseEvent x => new QGroupMemberIncrease
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId,
            InvitorId = x.InvitorId
        },
        Mk.GroupMemberDecreaseEvent x => new QGroupMemberDecrease
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId
        },
        Mk.FriendRequestEvent x => new QFriendRequestReceived
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            InitiatorId = x.InitiatorId,
            InitiatorUid = x.InitiatorUid,
            Comment = x.Comment,
            Via = x.Via
        },
        Mk.GroupInvitationEvent x => new QGroupInvitation
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            InvitationSeq = x.InvitationSeq,
            InitiatorId = x.InitiatorId,
            SourceGroupId = x.SourceGroupId
        },
        Mk.BotOfflineEvent x => new QBotOffline
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            Reason = x.Reason
        },
        Mk.FriendNudgeEvent x => new QFriendNudge
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            UserId = x.UserId,
            IsSelfSend = x.IsSelfSend,
            IsSelfReceive = x.IsSelfReceive,
            DisplayAction = x.DisplayAction,
            DisplaySuffix = x.DisplaySuffix,
            DisplayActionImgUrl = string.IsNullOrEmpty(x.DisplayActionImgUrl) ? null : x.DisplayActionImgUrl
        },
        Mk.FriendFileUploadEvent x => new QFriendFileUpload
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
        Mk.GroupAdminChangeEvent x => new QGroupAdminChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId,
            IsSet = x.IsSet
        },
        Mk.GroupEssenceMessageChangeEvent x => new QGroupEssenceMessageChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            MessageSeq = x.MessageSeq,
            OperatorId = x.OperatorId,
            IsSet = x.IsSet
        },
        Mk.GroupNameChangeEvent x => new QGroupNameChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NewGroupName = x.NewGroupName,
            OperatorId = x.OperatorId
        },
        Mk.GroupMessageReactionEvent x => new QGroupMessageReaction
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            MessageSeq = x.MessageSeq,
            FaceId = x.FaceId,
            ReactionType = x.ReactionType switch
            {
                Mk.GroupMessageReactionEventReactionType.Emoji => QReactionType.Emoji,
                _ => QReactionType.Face
            },
            IsAdd = x.IsAdd
        },
        Mk.GroupMuteEvent x => new QGroupMute
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            OperatorId = x.OperatorId,
            Duration = TimeSpan.FromSeconds(x.Duration)
        },
        Mk.GroupWholeMuteEvent x => new QGroupWholeMute
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            OperatorId = x.OperatorId,
            IsMute = x.IsMute
        },
        Mk.GroupNudgeEvent x => new QGroupNudge
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            SenderId = x.SenderId,
            ReceiverId = x.ReceiverId,
            DisplayAction = x.DisplayAction,
            DisplaySuffix = x.DisplaySuffix,
            DisplayActionImgUrl = string.IsNullOrEmpty(x.DisplayActionImgUrl) ? null : x.DisplayActionImgUrl
        },
        Mk.GroupFileUploadEvent x => new QGroupFileUpload
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            UserId = x.UserId,
            FileId = x.FileId,
            FileName = x.FileName,
            FileSize = x.FileSize
        },
        Mk.GroupJoinRequestEvent x => new QGroupJoinRequest
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NotificationSeq = x.NotificationSeq,
            InitiatorId = x.InitiatorId,
            Comment = x.Comment,
            IsFiltered = x.IsFiltered
        },
        Mk.GroupInvitedJoinRequestEvent x => new QGroupInvitedJoinRequest
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            NotificationSeq = x.NotificationSeq,
            InitiatorId = x.InitiatorId,
            TargetUserId = x.TargetUserId
        },
        Mk.GroupDisbandEvent x => new QGroupDisband
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            GroupId = x.GroupId,
            OperatorId = x.OperatorId
        },
        Mk.PeerPinChangeEvent x => new QPeerPinChange
        {
            Time = DateTimeOffset.FromUnixTimeSeconds(x.Time),
            SelfId = x.SelfId,
            Scene = x.MessageScene switch
            {
                Mk.PeerPinChangeEventMessageScene.Group => QMessageScene.Group,
                Mk.PeerPinChangeEventMessageScene.Temp => QMessageScene.Temp,
                _ => QMessageScene.Friend
            },
            PeerId = x.PeerId,
            IsPinned = x.IsPinned
        },
        _ => null
    };
}
