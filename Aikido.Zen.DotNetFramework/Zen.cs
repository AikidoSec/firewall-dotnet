using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using Aikido.Zen.DotNetFramework.Patches;
using System.Web;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetFramework.Configuration;
using Aikido.Zen.Core.Helpers;

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
            // set zen version
            AgentInfoHelper.SetVersion(typeof(Zen).Assembly.GetName().Version.ToString());
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

        internal static Func<HttpContext, User> SetUserAction { get; set; } = (context) => !string.IsNullOrEmpty(context.User.Identity?.Name)
            // if we have an identity, set the user to that identity automatically
            ? new User(context.User.Identity.Name, context.User.Identity.Name)
            // otherwise, return null
            : null;

        public static void SetUser(Func<HttpContext, User> setUser)
        {
            SetUserAction = setUser;
        }

        public static Context GetContext()
        {
            return (Context)HttpContext.Current?.Items["Aikido.Zen.Context"];
        }

        public static User GetUser()
        {
            return (User)HttpContext.Current?.Items["Aikido.Zen.CurrentUser"];
        }
    }
}
