namespace TCMD.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class DatabaseIntegrationCollection : ICollectionFixture<TcmdApiFactory>
{
    public const string Name = "Database integration";
}
