using ShiroBot.MilkyAdapter.Milky;
using ShiroBot.SDK.Adapter;

namespace ShiroBot.MilkyAdapter.AdapterImpl;

public class EventService : IEventService
{
    private bool _attached;

    public event Func<Event, Task>? EventReceived;

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
        var handlers = EventReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (Func<Event, Task> handler in handlers.GetInvocationList())
        {
            await handler(e);
        }
    }
}
