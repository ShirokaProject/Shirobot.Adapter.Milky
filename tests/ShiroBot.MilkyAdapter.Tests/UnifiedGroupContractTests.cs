using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.Model.QQ;
using Xunit;
using Mk = ShiroBot.Adapter.Milky.Model.Common;

namespace ShiroBot.MilkyAdapter.Tests;

public sealed class UnifiedGroupContractTests
{
    [Fact]
    public void Request_preserves_large_numeric_ids_as_strings()
    {
        var request = Assert.IsType<QGroupJoinRequest>(QModelMapper.ToQqEventPayload(
            new Mk.GroupJoinRequestEvent(100, long.MaxValue, 300, long.MaxValue, true, 500, "hello")));
        Assert.Equal("9223372036854775807", request.SelfId);
        Assert.Equal("9223372036854775807", request.RequestId);
        Assert.Equal("300", request.GroupId);
        Assert.Equal("500", request.UserId);
        Assert.Equal("hello", request.Comment);
        Assert.True(request.IsFiltered);
        Assert.False(request.IsInvited);
    }

    [Fact]
    public void Invited_request_identifies_the_invitee_and_inviter_separately()
    {
        var request = Assert.IsType<QGroupJoinRequest>(QModelMapper.ToQqEventPayload(
            new Mk.GroupInvitedJoinRequestEvent(100, 200, 300, 400, 500, 600)));
        Assert.Equal("600", request.UserId);
        Assert.Equal("500", request.InviterId);
        Assert.Equal("400", request.RequestId);
        Assert.True(request.IsInvited);
    }
}
