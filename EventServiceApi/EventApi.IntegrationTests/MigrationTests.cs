using System.Data.Common;

using EventApi.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

/// <summary>
/// Проверяет, что после Database.MigrateAsync() созданы ожидаемые таблицы и внешние ключи, а не только объявлены в EF-модели.
/// </summary>
public sealed class MigrationTests : RepositoryTestBase
{
    public MigrationTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Theory]
    [InlineData("events")]
    [InlineData("bookings")]
    public async Task Migrate_CreatesExpectedTable(string tableName)
    {
        await using var context = CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        var tables = await GetTableNamesAsync(connection);

        Assert.Contains(tableName, tables);
    }

    [Fact]
    public async Task Migrate_CreatesForeignKeyFromBookingsToEvents()
    {
        await using var context = CreateContext();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ccu.table_name AS referenced_table,
                ccu.column_name AS referenced_column,
                rc.delete_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON kcu.constraint_name = tc.constraint_name
            JOIN information_schema.constraint_column_usage ccu
                ON ccu.constraint_name = tc.constraint_name
            JOIN information_schema.referential_constraints rc
                ON rc.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_name = 'bookings'
              AND kcu.column_name = 'EventId';
            """;

        await using var reader = await command.ExecuteReaderAsync();
        var found = await reader.ReadAsync();

        Assert.True(found, "Ожидался внешний ключ bookings.EventId -> events.Id, но он не найден в схеме БД.");
        Assert.Equal("events", reader.GetString(reader.GetOrdinal("referenced_table")));
        Assert.Equal("Id", reader.GetString(reader.GetOrdinal("referenced_column")));
        Assert.Equal("CASCADE", reader.GetString(reader.GetOrdinal("delete_rule")));
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
