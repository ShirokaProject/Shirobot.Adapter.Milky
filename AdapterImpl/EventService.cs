using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.Model.Common;
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

    private async Task OnEventReceivedAsync(Event e)
    {
        switch (e)
        {
            case GroupIncomingMessage groupMessage when GroupMessageReceived is not null:
                await GroupMessageReceived(groupMessage);
                break;
            case FriendIncomingMessage friendMessage when FriendMessageReceived is not null:
                await FriendMessageReceived(friendMessage);
                break;
            case MessageRecallEvent messageRecall when MessageRecall is not null:
                await MessageRecall(messageRecall);
                break;
            case FriendRequestEvent friendRequest when FriendRequest is not null:
                await FriendRequest(friendRequest);
                break;
            case GroupJoinRequestEvent groupJoinRequest when GroupJoinRequest is not null:
                await GroupJoinRequest(groupJoinRequest);
                break;
            case GroupInvitedJoinRequestEvent invitedJoinRequest when GroupInvitedJoinRequest is not null:
                await GroupInvitedJoinRequest(invitedJoinRequest);
                break;
            case GroupInvitationEvent groupInvitation when GroupInvitation is not null:
                await GroupInvitation(groupInvitation);
                break;
            case FriendNudgeEvent friendNudge when FriendNudge is not null:
                await FriendNudge(friendNudge);
                break;
            case FriendFileUploadEvent friendFileUpload when FriendFileUpload is not null:
                await FriendFileUpload(friendFileUpload);
                break;
            case GroupAdminChangeEvent adminChange when GroupAdminChange is not null:
                await GroupAdminChange(adminChange);
                break;
            case GroupEssenceMessageChangeEvent essenceChange when GroupEssenceMessageChange is not null:
                await GroupEssenceMessageChange(essenceChange);
                break;
            case GroupMemberIncreaseEvent memberIncrease when GroupMemberIncrease is not null:
                await GroupMemberIncrease(memberIncrease);
                break;
            case GroupMemberDecreaseEvent memberDecrease when GroupMemberDecrease is not null:
                await GroupMemberDecrease(memberDecrease);
                break;
            case GroupNameChangeEvent nameChange when GroupNameChange is not null:
                await GroupNameChange(nameChange);
                break;
            case GroupMessageReactionEvent reactionEvent when GroupMessageReaction is not null:
                await GroupMessageReaction(reactionEvent);
                break;
            case GroupMuteEvent muteEvent when GroupMute is not null:
                await GroupMute(muteEvent);
                break;
            case GroupWholeMuteEvent wholeMuteEvent when GroupWholeMute is not null:
                await GroupWholeMute(wholeMuteEvent);
                break;
            case GroupNudgeEvent groupNudge when GroupNudge is not null:
                await GroupNudge(groupNudge);
                break;
            case GroupFileUploadEvent groupFileUpload when GroupFileUpload is not null:
                await GroupFileUpload(groupFileUpload);
                break;
        }
    }
}
