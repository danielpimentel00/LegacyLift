namespace LegacyLift.IntegrationTests;

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ContainersWebApplicationFactory>
{
    public const string Name = "Api";
}
