namespace LegacyLift.IntegrationTests;

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<SqliteWebApplicationFactory>
{
    public const string Name = "Api";
}