using System;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;

[assembly: PreApplicationStartMethod(typeof(ModuleRegistration), "RegisterModules")]
public static class ModuleRegistration
{
    public static void RegisterModules()
    {
        try
        {
            Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility.RegisterModule(typeof(Aikido.Zen.DotNetFramework.HttpModules.ContextModule));
            LogHelper.DebugLog(Agent.Logger, "Registered ContextModule");
            Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility.RegisterModule(typeof(Aikido.Zen.DotNetFramework.HttpModules.BlockingModule));
            LogHelper.DebugLog(Agent.Logger, "Registered BlockingModule");
        }
        catch (Exception ex)
        {
            LogHelper.ErrorLog(Agent.Logger, $"Error registering modules: {ex.Message}");
        }
    }
}
