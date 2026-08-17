using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventService.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAndBookingUserId : Migration
    {
        // Id системного пользователя-заглушки, на которого мапятся брони,
        // созданные до появления таблицы users (см. Up()).
        private static readonly Guid LegacyBookingsSystemUserId = new("00000000-0000-0000-0000-000000000001");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Login = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Login",
                table: "users",
                column: "Login",
                unique: true);

            // Системный пользователь должен существовать до того, как AddColumn ниже
            // проставит его id как default всем уже существующим броням — иначе FK
            // на непустой БД (например, проде) не сможет создаться.
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "Login", "PasswordHash", "Role" },
                values: new object[]
                {
                    LegacyBookingsSystemUserId,
                    "system.legacy-bookings",
                    "SYSTEM_ACCOUNT_NO_PASSWORD_LOGIN_DISABLED",
                    "User"
                });

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "bookings",
                type: "uuid",
                nullable: false,
                defaultValue: LegacyBookingsSystemUserId);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId",
                table: "bookings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_users_UserId",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "IX_bookings_UserId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "bookings");
        }
    }
}
