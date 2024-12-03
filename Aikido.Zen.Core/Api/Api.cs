using System;
using System.Collections.Generic;
using System.Text;

namespace Aikido.Zen.Core.Api
{
	public class ZenApi : IZenApi
	{
		public ZenApi(IReportingAPIClient reporting)
		{
			Reporting = reporting;
		}
		public IReportingAPIClient Reporting { get; private set; }
	}
}
