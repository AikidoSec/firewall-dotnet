using System.Text.Json;
using Aikido.Zen.Server.Mock.Models;

namespace Aikido.Zen.Server.Mock.Services;

public class ConfigService
{
    private readonly Dictionary<int, Dictionary<string, object>> _configs = new();
    private readonly Dictionary<int, IEnumerable<FirewallListConfig.IPList>> _blockedIps = new();
    private readonly Dictionary<int, string> _blockedUserAgents = new();
    private readonly Dictionary<int, IEnumerable<FirewallListConfig.IPList>> _allowedIps = new();

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
            _configs[appId][key] = value is JsonElement jsonElement
                ? ConvertJsonElement(jsonElement)
                : value;
        }

        _configs[appId]["configUpdatedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateBlockedIps(int appId, List<FirewallListConfig.IPList> ips)
    {
        _blockedIps[appId] = ips;
        UpdateConfigTimestamp(appId);
    }

    public void UpdateAllowedIps(int appId, List<FirewallListConfig.IPList> ips)
    {
        _allowedIps[appId] = ips;
        UpdateConfigTimestamp(appId);
    }

    public IEnumerable<FirewallListConfig.IPList> GetBlockedIps(int appId)
    {
        return _blockedIps.TryGetValue(appId, out var ips) ? ips : new List<FirewallListConfig.IPList>();
    }

    public IEnumerable<FirewallListConfig.IPList> GetAllowedIps(int appId)
    {
        return _allowedIps.TryGetValue(appId, out var ips) ? ips : new List<FirewallListConfig.IPList>();
    }

    public void UpdateBlockedUserAgents(int appId, string userAgents)
    {
        _blockedUserAgents[appId] = userAgents;
        UpdateConfigTimestamp(appId);
    }

    public string GetBlockedUserAgents(int appId)
    {
        return _blockedUserAgents.TryGetValue(appId, out var agents) ? agents : string.Empty;
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
            ["allowedIPAddresses"] = _largeBlockedIpList,
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

    private static List<string> _largeBlockedIpList = Enumerable.Range(0, 1000)
        .Select(i => i < 500
            // Generate IPv4 addresses (first 500)
            ? $"{(i >> 24) & 0xFF}.{(i >> 16) & 0xFF}.{(i >> 8) & 0xFF}.{i & 0xFF}"
            // Generate IPv6 addresses (last 500)
            : $"2001:db8:{((i - 500) >> 8):x}::{(i - 500) & 0xFF:x}")
        .ToList();

    private object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray()
                .Select(x => x is JsonElement je ? ConvertJsonElement(je) : x)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    x => x.Name,
                    x => x.Value is JsonElement je ? ConvertJsonElement(je) : x.Value
                ),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.Null => null,
            _ => element.GetString()
        };
    }
}
