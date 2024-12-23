using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Patches;
using System.Web;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetFramework.Configuration;

namespace Aikido.Zen.DotNetFramework
{
    public class Zen
    {
        // we need to reference Harmony somewhere to ensure it is copied with our package
        private static HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("reference");
        public static void Start()
        {
            // initialize the options, this will ensure the environment variables are set
            AikidoConfiguration.Init();
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return;
            }
            // patch the sinks
            Patcher.Patch();
            // setup the agent
            if (Agent.Instance == null)
            {
                var reportingApiClient = new ReportingAPIClient();
                var runtimeApiClient = new RuntimeAPIClient();
                var zenApi = new ZenApi(reportingApiClient, runtimeApiClient);
                Agent.NewInstance(zenApi);
            }
            Agent.Instance.Start();
        }

        internal static Func<HttpContext, User> SetUserAction { get; set; } = (context) => !string.IsNullOrEmpty(context.User.Identity?.Name) ? new User(context.User.Identity?.Name ?? context.Session.SessionID, context.User.Identity?.Name ?? "Anonymous") : null;

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
