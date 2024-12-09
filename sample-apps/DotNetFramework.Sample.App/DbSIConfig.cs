namespace DotNetFramework.Sample.App
{
	public class DbSIConfig
	{
		public string Name { get; set; }
		public string ConnectionString { get; set; }
		public string InjectionQuery { get; set; }
		public string CreateTableQuery { get; set; }
		public string CreateDbIfNotExistsQuery { get; set; }
		public string SeedTableQuery { get; set; }
		public string DropTableQuery { get; set; }
	}
}
