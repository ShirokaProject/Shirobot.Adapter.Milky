using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class EventService : IEventService
{
    private bool _attached;

    public event Func<GroupIncomingMessage, Task>? GroupMessageReceived;
    public event Func<FriendIncomingMessage, Task>? FriendMessageReceived;
    public event Func<MessageRecallEvent, Task>? MessageRecall;
    public event Func<FriendRequestEvent, Task>? FriendRequest;
    public event Func<GroupJoinRequestEvent, Task>? GroupJoinRequest;
    public event Func<GroupInvitedJoinRequestEvent, Task>? GroupInvitedJoinRequest;
    public event Func<GroupInvitationEvent, Task>? GroupInvitation;
    public event Func<FriendNudgeEvent, Task>? FriendNudge;
    public event Func<FriendFileUploadEvent, Task>? FriendFileUpload;
    public event Func<GroupAdminChangeEvent, Task>? GroupAdminChange;
    public event Func<GroupEssenceMessageChangeEvent, Task>? GroupEssenceMessageChange;
    public event Func<GroupMemberIncreaseEvent, Task>? GroupMemberIncrease;
    public event Func<GroupMemberDecreaseEvent, Task>? GroupMemberDecrease;
    public event Func<GroupNameChangeEvent, Task>? GroupNameChange;
    public event Func<GroupMessageReactionEvent, Task>? GroupMessageReaction;
    public event Func<GroupMuteEvent, Task>? GroupMute;
    public event Func<GroupWholeMuteEvent, Task>? GroupWholeMute;
    public event Func<GroupNudgeEvent, Task>? GroupNudge;
    public event Func<GroupFileUploadEvent, Task>? GroupFileUpload;
    public event Func<GroupDisbandEvent, Task>? GroupDisband;
    public event Func<PeerPinChangeEvent, Task>? PeerPinChange; 
    public event Func<BotOfflineEvent, Task>? BotOffline;

    private static MilkyClient Milky => MilkyClientManager.Instance;

    public void AttachEvent()
    {
        if (_attached)
        {
            return;
        }

        Milky.EventReceived += OnEventReceivedAsync;
        _attached = true;
    }

    internal async Task OnEventReceivedAsync(Event e)
    {
        switch (e)
        {
            case GroupIncomingMessage groupMessage:
                await InvokeSubscribersAsync(GroupMessageReceived, groupMessage);
                break;
            case FriendIncomingMessage friendMessage:
                await InvokeSubscribersAsync(FriendMessageReceived, friendMessage);
                break;
            case MessageRecallEvent messageRecall:
                await InvokeSubscribersAsync(MessageRecall, messageRecall);
                break;
            case FriendRequestEvent friendRequest:
                await InvokeSubscribersAsync(FriendRequest, friendRequest);
                break;
            case GroupJoinRequestEvent groupJoinRequest:
                await InvokeSubscribersAsync(GroupJoinRequest, groupJoinRequest);
                break;
            case GroupInvitedJoinRequestEvent invitedJoinRequest:
                await InvokeSubscribersAsync(GroupInvitedJoinRequest, invitedJoinRequest);
                break;
            case GroupInvitationEvent groupInvitation:
                await InvokeSubscribersAsync(GroupInvitation, groupInvitation);
                break;
            case FriendNudgeEvent friendNudge:
                await InvokeSubscribersAsync(FriendNudge, friendNudge);
                break;
            case FriendFileUploadEvent friendFileUpload:
                await InvokeSubscribersAsync(FriendFileUpload, friendFileUpload);
                break;
            case GroupAdminChangeEvent adminChange:
                await InvokeSubscribersAsync(GroupAdminChange, adminChange);
                break;
            case GroupEssenceMessageChangeEvent essenceChange:
                await InvokeSubscribersAsync(GroupEssenceMessageChange, essenceChange);
                break;
            case GroupMemberIncreaseEvent memberIncrease:
                await InvokeSubscribersAsync(GroupMemberIncrease, memberIncrease);
                break;
            case GroupMemberDecreaseEvent memberDecrease:
                await InvokeSubscribersAsync(GroupMemberDecrease, memberDecrease);
                break;
            case GroupNameChangeEvent nameChange:
                await InvokeSubscribersAsync(GroupNameChange, nameChange);
                break;
            case GroupMessageReactionEvent reactionEvent:
                await InvokeSubscribersAsync(GroupMessageReaction, reactionEvent);
                break;
            case GroupMuteEvent muteEvent:
                await InvokeSubscribersAsync(GroupMute, muteEvent);
                break;
            case GroupWholeMuteEvent wholeMuteEvent:
                await InvokeSubscribersAsync(GroupWholeMute, wholeMuteEvent);
                break;
            case GroupNudgeEvent groupNudge:
                await InvokeSubscribersAsync(GroupNudge, groupNudge);
                break;
            case GroupFileUploadEvent groupFileUpload:
                await InvokeSubscribersAsync(GroupFileUpload, groupFileUpload);
                break;
            case GroupDisbandEvent groupDisband:
                await InvokeSubscribersAsync(GroupDisband, groupDisband);
                break;
            case PeerPinChangeEvent peerPinChange:
                await InvokeSubscribersAsync(PeerPinChange, peerPinChange);
                break;
            case BotOfflineEvent botOffline:
                await InvokeSubscribersAsync(BotOffline, botOffline);
                break;
        }
    }

    private static async Task InvokeSubscribersAsync<TEvent>(Func<TEvent, Task>? handlers, TEvent data)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var subscriber in handlers.GetInvocationList().Cast<Func<TEvent, Task>>())
        {
            await subscriber(data);
        }
    }
}
