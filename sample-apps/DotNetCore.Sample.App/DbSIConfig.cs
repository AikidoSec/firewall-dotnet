namespace DotNetCore.Sample.App;
/// <summary>
/// Configuration for a database with SQL injection vulnerability
/// </summary>
public class DbSIConfig
{
    public required string Name { get; set; }
    public required string ConnectionString { get; set; }
    public required string InjectionQuery { get; set; }
    public required string CreateTableQuery { get; set; }
    public required string CreateDbIfNotExistsQuery { get; set; }
    public required string SeedTableQuery { get; set; }
    public required string DropTableQuery { get; set; }
}
