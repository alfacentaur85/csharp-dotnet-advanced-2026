using System.Data.Common;

using EventApi.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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

    [Fact]
    public async Task Migrate_AddUserAndBookingUserId_BackfillsExistingBookingsWithValidUser()
    {
        // Накатываем только InitialCreate, чтобы получить bookings без колонки UserId —
        // так же, как на проде до применения AddUserAndBookingUserId.
        await using (var context = CreateContext())
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260725160104_InitialCreate");
        }

        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        await using (var context = CreateContext())
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var insertEvent = connection.CreateCommand();
            insertEvent.CommandText = """
                INSERT INTO events ("Id", "Title", "StartAt", "EndAt", "TotalSeats", "AvailableSeats")
                VALUES (@id, 'Legacy event', now() + interval '1 day', now() + interval '2 day', 10, 9);
                """;
            insertEvent.Parameters.Add(new Npgsql.NpgsqlParameter("id", eventId));
            await insertEvent.ExecuteNonQueryAsync();

            await using var insertBooking = connection.CreateCommand();
            insertBooking.CommandText = """
                INSERT INTO bookings ("Id", "EventId", "Status", "CreatedAt")
                VALUES (@id, @eventId, 'Confirmed', now());
                """;
            insertBooking.Parameters.Add(new Npgsql.NpgsqlParameter("id", bookingId));
            insertBooking.Parameters.Add(new Npgsql.NpgsqlParameter("eventId", eventId));
            await insertBooking.ExecuteNonQueryAsync();
        }

        // Накатываем оставшиеся миграции на непустую таблицу bookings — раньше здесь падал FK.
        await using (var context = CreateContext())
        {
            var migrator = context.Database.GetService<IMigrator>();
            await migrator.MigrateAsync();
        }

        await using (var context = CreateContext())
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT b."UserId"
                FROM bookings b
                JOIN users u ON u."Id" = b."UserId"
                WHERE b."Id" = @id;
                """;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("id", bookingId));

            var userId = await command.ExecuteScalarAsync();

            Assert.NotNull(userId);
            Assert.NotEqual(Guid.Empty, (Guid)userId!);
        }
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
