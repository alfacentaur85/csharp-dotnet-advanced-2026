using System.Data.Common;

using Events.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Events.IntegrationTests;

/// <summary>
/// Проверяет, что после Database.MigrateAsync() создана ожидаемая таблица, а не только объявлена в EF-модели.
/// </summary>
public sealed class MigrationTests : RepositoryTestBase
{
    public MigrationTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Migrate_CreatesEventsTable()
    {
        await using var context = CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        var tables = await GetTableNamesAsync(connection);

        Assert.Contains("events", tables);
    }

    private static async Task<List<string>> GetTableNamesAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public' AND table_type = 'BASE TABLE';
            """;

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
