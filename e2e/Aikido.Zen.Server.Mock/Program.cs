using Aikido.Zen.Server.Mock;
using Aikido.Zen.Server.Mock.Filters;
using Aikido.Zen.Server.Mock.Models;
using Aikido.Zen.Server.Mock.Services;

var startup = new MockServerStartup();
var app = startup.BuildAndRun(args);
await app.RunAsync();
