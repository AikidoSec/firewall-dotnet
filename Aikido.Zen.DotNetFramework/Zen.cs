using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Configuration;
using Aikido.Zen.DotNetFramework.Patches;
using System.Web;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.DotNetFramework
{
    public class Zen
    {
        // we need to reference Harmony somewhere to ensure it is copied with our package
        private static HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("reference");
        public static void Start()
        {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return;
            }
            // patch the sinks
            Patcher.Patch();
            // setup the agent
            if (Agent.Instance == null)
            {
                var baseUrl = Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev";
                var runtimeUrl = Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL") ?? "https://runtime.aikido.dev";
                var aikidoUrl = new Uri(baseUrl);
                var runtimeUri = new Uri(runtimeUrl);
                var reportingApiClient = new ReportingAPIClient(aikidoUrl);
                var runtimeApiClient = new RuntimeAPIClient(runtimeUri, aikidoUrl);
                var zenApi = new ZenApi(reportingApiClient, runtimeApiClient);
                Agent.GetInstance(zenApi);
            }
            Agent.Instance.Start();
        }

        internal static Func<HttpContext, User> SetUserAction { get; set; } = (context) => new User(context.User.Identity?.Name ?? context.Session.SessionID, context.User.Identity?.Name ?? "Anonymous");

        public static void SetUser(Func<HttpContext, User> setUser)
        {
            SetUserAction = setUser;
        }

        public static Context GetContext()
        {
            return (Context)HttpContext.Current.Items["Aikido.Zen.Context"];
        }

        public static User GetUser()
        {
            return (User)HttpContext.Current.Items["Aikido.Zen.CurrentUser"];
        }
    }
}
