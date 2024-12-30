using Aikido.Zen.Server.Mock.Models;

namespace Aikido.Zen.Server.Mock.Services;

public class ConfigService
{
    private readonly Dictionary<int, Dictionary<string, object>> _configs = new();
    private readonly Dictionary<int, List<string>> _blockedIps = new();
    private readonly Dictionary<int, List<string>> _blockedUserAgents = new();

    public Dictionary<string, object> GetConfig(int appId)
    {
        if (!_configs.TryGetValue(appId, out var config))
        {
            config = GenerateDefaultConfig(appId);
            _configs[appId] = config;
        }
        return config;
    }

    public void UpdateConfig(int appId, Dictionary<string, object> newConfig)
    {
        if (!_configs.ContainsKey(appId))
        {
            _configs[appId] = GenerateDefaultConfig(appId);
        }
        
        foreach (var (key, value) in newConfig)
        {
            _configs[appId][key] = value;
        }
        
        _configs[appId]["configUpdatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateBlockedIps(int appId, List<string> ips)
    {
        _blockedIps[appId] = ips;
        UpdateConfigTimestamp(appId);
    }

    public List<string> GetBlockedIps(int appId)
    {
        return _blockedIps.TryGetValue(appId, out var ips) ? ips : new List<string>();
    }

    public void UpdateBlockedUserAgents(int appId, string userAgents)
    {
        _blockedUserAgents[appId] = userAgents.Split('\n').ToList();
        UpdateConfigTimestamp(appId);
    }

    public List<string> GetBlockedUserAgents(int appId)
    {
        return _blockedUserAgents.TryGetValue(appId, out var agents) ? agents : new List<string>();
    }

    private Dictionary<string, object> GenerateDefaultConfig(int appId)
    {
        return new Dictionary<string, object>
        {
            ["success"] = true,
            ["serviceId"] = appId,
            ["configUpdatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["heartbeatIntervalInMS"] = 10 * 60 * 1000,
            ["endpoints"] = new List<EndpointConfig>(),
            ["blockedUserIds"] = new List<string>(),
            ["allowedIPAddresses"] = new List<string>(),
            ["receivedAnyStats"] = true
        };
    }

    private void UpdateConfigTimestamp(int appId)
    {
        if (_configs.TryGetValue(appId, out var config))
        {
            config["configUpdatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
} 