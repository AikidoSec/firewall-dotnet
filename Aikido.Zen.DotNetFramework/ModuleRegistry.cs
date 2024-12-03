using System.Web;

[assembly: PreApplicationStartMethod(typeof(ModuleRegistration), "RegisterModules")]
public static class ModuleRegistration
{
	public static void RegisterModules()
	{
		Microsoft.Web.Infrastructure.DynamicModuleHelper.DynamicModuleUtility.RegisterModule(typeof(Aikido.Zen.DotNetFramework.HttpModules.ContextModule));
	}
}
