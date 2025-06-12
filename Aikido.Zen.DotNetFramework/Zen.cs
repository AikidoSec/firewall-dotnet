using System;
using System.Linq;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetFramework.Configuration;
using Aikido.Zen.DotNetFramework.HttpModules;
using Aikido.Zen.DotNetFramework.Patches;

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
            // making sure the http modules are installed
            CheckModules();
            Agent.Instance.Start();
            EnvironmentHelper.ReportValues();
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

        internal static void CheckModules()
        {
            var isContextModuleInstalled = HttpContext.Current.ApplicationInstance.Modules.AllKeys.Any(key => key.Contains("Aikido.Zen.DotNetFramework.HttpModules.ContextModule"));
            var isBlockingModuleInstalled = HttpContext.Current.ApplicationInstance.Modules.AllKeys.Any(key => key.Contains("Aikido.Zen.DotNetFramework.HttpModules.BlockingModule"));

            if (!isContextModuleInstalled)
            {
                LogHelper.DebugLog(Agent.Logger, "Aikido.Zen.DotNetFramework.HttpModules.ContextModule is not installed, try calling Zen.Init() from inside the Global.asax.cs public override void Init() method or register the module in your web.config.");
            }
            if (!isBlockingModuleInstalled)
            {
                LogHelper.DebugLog(Agent.Logger, "Aikido.Zen.DotNetFramework.HttpModules.BlockingModule is not installed, try calling Zen.Init() from inside the Global.asax.cs public override void Init() method or register the module in your web.config.");
            }
        }

        internal static void RegisterModules()
        {
            LogHelper.DebugLog(Agent.Logger, "Registering Zen modules");
            var contextModule = new ContextModule();
            var blockingModule = new BlockingModule();
            try
            {
                contextModule.Init(HttpContext.Current.ApplicationInstance);
                blockingModule.Init(HttpContext.Current.ApplicationInstance);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, "Error initializing Zen modules: " + ex.Message);
                throw;
            }
        }


        public static void Init()
        {
            LogHelper.DebugLog(Agent.Logger, "Initializing the Zen modules manually");
            RegisterModules();
        }
    }
}
