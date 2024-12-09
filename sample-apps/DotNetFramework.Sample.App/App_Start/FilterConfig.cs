using System.Web;
using System.Web.Mvc;

namespace DotNetFramework.Sample.App
{
	public class FilterConfig
	{
		public static void RegisterGlobalFilters(GlobalFilterCollection filters)
		{
			filters.Add(new HandleErrorAttribute());
		}
	}
}
