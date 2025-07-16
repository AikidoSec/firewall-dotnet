namespace Aikido.Zen.Server.Mock.Services;

public class EventService
{
    private readonly Dictionary<int, List<Dictionary<string, object>>> _events = new();

    public void CaptureEvent(int appId, Dictionary<string, object> eventData)
    {
        if (!_events.ContainsKey(appId))
        {
            _events[appId] = new List<Dictionary<string, object>>();
        }

        if (eventData.TryGetValue("type", out var type) && type?.ToString() != "heartbeat")
        {
            _events[appId].Add(eventData);
        }
        else if (type?.ToString() == "heartbeat")
        {
            _events[appId].Add(eventData);
        }
    }

    public List<Dictionary<string, object>> GetEvents(int appId)
    {
        return ListEvents(appId);
    }

    public void ClearEvents(int appId)
    {
        if (_events.ContainsKey(appId))
        {
            _events[appId].Clear();
        }
    }

    public List<Dictionary<string, object>> ListEvents(int appId)
    {
        return _events.TryGetValue(appId, out var events) ? events : new List<Dictionary<string, object>>();
    }
}
