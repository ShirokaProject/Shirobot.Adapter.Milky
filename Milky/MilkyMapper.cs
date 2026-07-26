using ShiroBot.Qq.Model;
using ShiroBot.SDK.Models;
using Sdk = ShiroBot.SDK.Models;
using Mk = ShiroBot.Model.Common;

namespace ShiroBot.MilkyAdapter.Milky;

/// <summary>
/// Milky(QQ)协议模型 ↔ ShiroBot 平台无关核心模型的双向映射。
/// </summary>
internal static class MilkyMapper
{
    public const string PlatformId = "qq";

    // ─── Channel ───

    public static Sdk.Channel GroupChannel(long groupId, string? name = null) =>
        new(groupId.ToString(), ChannelType.Group) { Name = name, GuildId = groupId.ToString() };

    public static Sdk.Channel DirectChannel(long userId) => Sdk.Channel.Direct(userId.ToString());

    /// <summary>QQ 临时会话。Id 为对方用户 ID,GuildId 为来源群。</summary>
    public static Sdk.Channel TempChannel(long peerId, long? groupId) =>
        new(peerId.ToString(), ChannelType.Other) { GuildId = groupId?.ToString() };

    // ─── 实体 ───

    public static Sdk.User ToUser(Mk.FriendEntity friend) =>
        new(friend.UserId.ToString()) { Name = friend.Nickname };

    public static Sdk.User ToUser(Mk.GroupMemberEntity member) =>
        new(member.UserId.ToString()) { Name = member.Nickname };

    public static Sdk.Member ToMember(Mk.GroupMemberEntity member) =>
        new(ToUser(member))
        {
            Nick = string.IsNullOrEmpty(member.Card) ? null : member.Card,
            Role = member.Role switch
            {
                Mk.GroupMemberEntityRole.Owner => MemberRole.Owner,
                Mk.GroupMemberEntityRole.Admin => MemberRole.Admin,
                _ => MemberRole.Member
            },
            JoinedAt = DateTimeOffset.FromUnixTimeSeconds(member.JoinTime)
        };

    public static Sdk.Channel ToChannel(Mk.GroupEntity group) =>
        GroupChannel(group.GroupId, group.GroupName);

    // ─── 入站消息 ───

    public static MessageEvent? ToMessageEvent(Mk.IncomingMessage message) => message switch
    {
        Mk.FriendIncomingMessage friend => new MessageEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = QqModelMapper.ToQq(friend),
            MessageId = friend.MessageSeq.ToString(),
            Channel = DirectChannel(friend.PeerId),
            Sender = ToUser(friend.Friend),
            Segments = ToSegments(friend.Segments),
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(friend.Time)
        },
        Mk.GroupIncomingMessage group => new MessageEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = QqModelMapper.ToQq(group),
            MessageId = group.MessageSeq.ToString(),
            Channel = ToChannel(group.Group),
            Sender = ToUser(group.GroupMember),
            Member = ToMember(group.GroupMember),
            Segments = ToSegments(group.Segments),
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(group.Time)
        },
        Mk.TempIncomingMessage temp => new MessageEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = QqModelMapper.ToQq(temp),
            MessageId = temp.MessageSeq.ToString(),
            Channel = TempChannel(temp.PeerId, temp.Group?.GroupId),
            Sender = new Sdk.User(temp.SenderId.ToString()),
            Segments = ToSegments(temp.Segments),
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(temp.Time)
        },
        _ => null
    };

    public static IReadOnlyList<MessageSegment> ToSegments(IReadOnlyList<Mk.IncomingSegment> segments) =>
        segments.Select(ToSegment).ToArray();

    private static MessageSegment ToSegment(Mk.IncomingSegment segment) => segment switch
    {
        Mk.TextIncomingSegment text => new TextSegment(text.Text),
        Mk.MentionIncomingSegment mention => new MentionSegment(mention.UserId.ToString()) { DisplayName = mention.Name },
        Mk.MentionAllIncomingSegment => new MentionAllSegment(),
        Mk.ReplyIncomingSegment reply => new QuoteSegment(reply.MessageSeq.ToString()),
        Mk.FaceIncomingSegment face => new EmojiSegment(face.FaceId),
        Mk.ImageIncomingSegment image => new ImageSegment(image.TempUrl)
        {
            ResourceId = image.ResourceId,
            Width = image.Width,
            Height = image.Height,
            Summary = image.Summary
        },
        Mk.RecordIncomingSegment record => new AudioSegment(record.TempUrl)
        {
            ResourceId = record.ResourceId,
            Duration = TimeSpan.FromSeconds(record.Duration)
        },
        Mk.VideoIncomingSegment video => new VideoSegment(video.TempUrl) { ResourceId = video.ResourceId },
        Mk.FileIncomingSegment file => new Sdk.FileSegment(string.Empty)
        {
            ResourceId = file.FileId,
            FileName = file.FileName,
            FileSize = file.FileSize
        },
        _ => new RawSegment(PlatformId, GetRawSegmentKind(segment), QqModelMapper.ToQq(segment))
    };

    private static string GetRawSegmentKind(Mk.IncomingSegment segment) => segment switch
    {
        Mk.ForwardIncomingSegment => "forward",
        Mk.MarketFaceIncomingSegment => "market_face",
        Mk.LightAppIncomingSegment => "light_app",
        Mk.XmlIncomingSegment => "xml",
        Mk.MarkdownIncomingSegment => "markdown",
        _ => segment.GetType().Name
    };

    // ─── 出站消息段 ───

    public static IReadOnlyList<Mk.OutgoingSegment> ToOutgoingSegments(IReadOnlyList<MessageSegment> segments) =>
        segments.Select(ToOutgoingSegment).ToArray();

    private static Mk.OutgoingSegment ToOutgoingSegment(MessageSegment segment) => segment switch
    {
        TextSegment text => new Mk.TextOutgoingSegment(text.Text),
        MentionSegment mention => new Mk.MentionOutgoingSegment(ParseId(mention.UserId, "MentionSegment.UserId")),
        MentionAllSegment => new Mk.MentionAllOutgoingSegment(),
        QuoteSegment quote => new Mk.ReplyOutgoingSegment(ParseId(quote.MessageId, "QuoteSegment.MessageId")),
        EmojiSegment emoji => new Mk.FaceOutgoingSegment(emoji.Id),
        ImageSegment image => new Mk.ImageOutgoingSegment(image.Uri) { Summary = image.Summary },
        AudioSegment audio => new Mk.RecordOutgoingSegment(audio.Uri),
        VideoSegment video => new Mk.VideoOutgoingSegment(video.Uri),
        RawSegment { Payload: QqOutgoingSegment qqOutgoing } => QqModelMapper.ToMilky(qqOutgoing),
        RawSegment { Payload: Mk.OutgoingSegment outgoing } => outgoing,
        RawSegment raw => throw new NotSupportedException(
            $"RawSegment(kind={raw.Kind}) 的 Payload 必须是 QqOutgoingSegment,实际为 {raw.Payload?.GetType().Name ?? "null"}。"),
        _ => throw new NotSupportedException($"Milky 适配器不支持发送 {segment.GetType().Name}。")
    };

    // ─── 事件 ───

    /// <summary>把 Milky 事件转为核心模型事件。返回 null 表示应忽略。</summary>
    public static Sdk.BotEvent? ToBotEvent(Mk.Event e) => e switch
    {
        Mk.IncomingMessage message => ToMessageEvent(message),

        Mk.MessageRecallEvent recall => new MessageDeletedEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = recall,
            MessageId = recall.MessageSeq.ToString(),
            Channel = recall.MessageScene == Mk.MessageRecallEventMessageScene.Group
                ? GroupChannel(recall.PeerId)
                : DirectChannel(recall.PeerId),
            SenderId = recall.SenderId.ToString(),
            OperatorId = recall.OperatorId.ToString()
        },

        Mk.GroupMemberIncreaseEvent increase => new MemberJoinedEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = increase,
            Channel = GroupChannel(increase.GroupId),
            UserId = increase.UserId.ToString(),
            OperatorId = (increase.OperatorId ?? increase.InvitorId)?.ToString()
        },

        Mk.GroupMemberDecreaseEvent decrease => new MemberLeftEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = decrease,
            Channel = GroupChannel(decrease.GroupId),
            UserId = decrease.UserId.ToString(),
            OperatorId = decrease.OperatorId?.ToString()
        },

        Mk.FriendRequestEvent friendRequest => new Sdk.FriendRequestEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = friendRequest,
            UserId = friendRequest.InitiatorId.ToString(),
            Comment = friendRequest.Comment,
            Token = friendRequest.InitiatorUid
        },

        Mk.GroupInvitationEvent invitation => new GuildInviteEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = invitation,
            GuildId = invitation.GroupId.ToString(),
            InviterId = invitation.InitiatorId.ToString(),
            Token = invitation.InvitationSeq.ToString()
        },

        Mk.BotOfflineEvent offline => new Sdk.BotOfflineEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = offline,
            Reason = offline.Reason
        },

        // 其余 QQ 特有事件统一走 PlatformEvent,Raw 为通用 QQ 模型负载(ShiroBot.Qq.Model)
        _ => new PlatformEvent
        {
            Platform = PlatformId,
            SelfId = MilkySession.SelfId,
            Raw = (object?)QqModelMapper.ToQqEventPayload(e) ?? e,
            Kind = GetPlatformEventKind(e),
            Channel = GetPlatformEventChannel(e)
        }
    };

    private static string GetPlatformEventKind(Mk.Event e) => e switch
    {
        Mk.FriendNudgeEvent => "friend_nudge",
        Mk.FriendFileUploadEvent => "friend_file_upload",
        Mk.GroupAdminChangeEvent => "group_admin_change",
        Mk.GroupEssenceMessageChangeEvent => "group_essence_message_change",
        Mk.GroupNameChangeEvent => "group_name_change",
        Mk.GroupMessageReactionEvent => "group_message_reaction",
        Mk.GroupMuteEvent => "group_mute",
        Mk.GroupWholeMuteEvent => "group_whole_mute",
        Mk.GroupNudgeEvent => "group_nudge",
        Mk.GroupFileUploadEvent => "group_file_upload",
        Mk.GroupJoinRequestEvent => "group_join_request",
        Mk.GroupInvitedJoinRequestEvent => "group_invited_join_request",
        Mk.GroupDisbandEvent => "group_disband",
        Mk.PeerPinChangeEvent => "peer_pin_change",
        _ => ToSnakeCase(e.GetType().Name.TrimEnd("Event"))
    };

    private static Sdk.Channel? GetPlatformEventChannel(Mk.Event e) => e switch
    {
        Mk.GroupAdminChangeEvent x => GroupChannel(x.GroupId),
        Mk.GroupEssenceMessageChangeEvent x => GroupChannel(x.GroupId),
        Mk.GroupNameChangeEvent x => GroupChannel(x.GroupId),
        Mk.GroupMessageReactionEvent x => GroupChannel(x.GroupId),
        Mk.GroupMuteEvent x => GroupChannel(x.GroupId),
        Mk.GroupWholeMuteEvent x => GroupChannel(x.GroupId),
        Mk.GroupNudgeEvent x => GroupChannel(x.GroupId),
        Mk.GroupFileUploadEvent x => GroupChannel(x.GroupId),
        Mk.GroupJoinRequestEvent x => GroupChannel(x.GroupId),
        Mk.GroupInvitedJoinRequestEvent x => GroupChannel(x.GroupId),
        Mk.GroupDisbandEvent x => GroupChannel(x.GroupId),
        Mk.FriendNudgeEvent x => DirectChannel(x.UserId),
        Mk.FriendFileUploadEvent x => DirectChannel(x.UserId),
        _ => null
    };

    // ─── 工具 ───

    public static long ParseId(string id, string what)
    {
        if (!long.TryParse(id, out var value))
        {
            throw new ArgumentException($"Milky(QQ) 平台的 {what} 必须是数字,实际为 '{id}'。");
        }

        return value;
    }

    private static string TrimEnd(this string value, string suffix) =>
        value.EndsWith(suffix, StringComparison.Ordinal) ? value[..^suffix.Length] : value;

    private static string ToSnakeCase(string value) =>
        string.Concat(value.Select((c, i) => char.IsUpper(c)
            ? (i > 0 ? "_" : string.Empty) + char.ToLowerInvariant(c)
            : c.ToString()));
}
