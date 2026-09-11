namespace Bookings.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres collection";
}
