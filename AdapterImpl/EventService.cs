using ShiroBot.Adapter.Milky.Milky;
using ShiroBot.SDK.Adapter;
using ShiroBot.SDK.Models;

namespace ShiroBot.Adapter.Milky.AdapterImpl;

/// <summary>把 Milky 事件流转换为平台无关 BotEvent 并上报宿主。</summary>
public class EventService : IEventService
{
    private bool _attached;

    public event Func<BotEvent, Task>? EventReceived;

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

    public void DetachEvent()
    {
        if (!_attached)
        {
            return;
        }

        Milky.EventReceived -= OnEventReceivedAsync;
        _attached = false;
    }

    private async Task OnEventReceivedAsync(Event e)
    {
        if (EventReceived is null)
        {
            return;
        }

        if (MilkyMapper.ToBotEvent(e) is { } botEvent)
        {
            await EventReceived(botEvent);
        }
    }
}
