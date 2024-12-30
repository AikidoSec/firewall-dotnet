using Aikido.Zen.Server.Mock.Models;

namespace Aikido.Zen.Server.Mock.Services;

public class AppService
{
    private readonly List<AppModel> _apps = new();
    private int _nextId = 1;

    public string CreateApp()
    {
        var appId = _nextId++;
        var token = AppModel.GenerateToken(appId);
        var app = new AppModel
        {
            Id = appId,
            Token = token,
            ConfigUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _apps.Add(app);
        return token;
    }

    public AppModel? GetByToken(string token)
    {
        return _apps.FirstOrDefault(app => AppModel.ValidateToken(app.Token, token));
    }
} 