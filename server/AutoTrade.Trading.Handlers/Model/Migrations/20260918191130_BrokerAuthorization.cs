using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class BrokerAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerAuthorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    CtidTraderAccountId = table.Column<long>(type: "INTEGER", nullable: true),
                    AccessTokenCipher = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshTokenCipher = table.Column<string>(type: "TEXT", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AuthorizedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerAuthorization", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrokerAuthorizationAttempt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationHash = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerAuthorizationAttempt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAuthorization_Environment",
                table: "BrokerAuthorization",
                column: "Environment",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAuthorizationAttempt_CorrelationHash",
                table: "BrokerAuthorizationAttempt",
                column: "CorrelationHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAuthorizationAttempt_ExpiresAtUtc",
                table: "BrokerAuthorizationAttempt",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerAuthorization");

            migrationBuilder.DropTable(
                name: "BrokerAuthorizationAttempt");
        }
    }
}
