using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class ComposedBasketStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CombinationMode",
                table: "BasketVersionPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConflictPolicy",
                table: "BasketVersionPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAgreement",
                table: "BasketVersionPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumStrategyConfidence",
                table: "BasketVersionPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CombinationMode",
                table: "BasketDraftPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ConflictPolicy",
                table: "BasketDraftPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumAgreement",
                table: "BasketDraftPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinimumStrategyConfidence",
                table: "BasketDraftPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BasketDraftStrategyComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDraftStrategyComponent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketVersionStrategyComponent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketVersionStrategyComponent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BasketDraftStrategyComponent_BasketId_Ordinal",
                table: "BasketDraftStrategyComponent",
                columns: new[] { "BasketId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketVersionStrategyComponent_VersionId_Ordinal",
                table: "BasketVersionStrategyComponent",
                columns: new[] { "VersionId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BasketDraftStrategyComponent");

            migrationBuilder.DropTable(
                name: "BasketVersionStrategyComponent");

            migrationBuilder.DropColumn(
                name: "CombinationMode",
                table: "BasketVersionPolicy");

            migrationBuilder.DropColumn(
                name: "ConflictPolicy",
                table: "BasketVersionPolicy");

            migrationBuilder.DropColumn(
                name: "MinimumAgreement",
                table: "BasketVersionPolicy");

            migrationBuilder.DropColumn(
                name: "MinimumStrategyConfidence",
                table: "BasketVersionPolicy");

            migrationBuilder.DropColumn(
                name: "CombinationMode",
                table: "BasketDraftPolicy");

            migrationBuilder.DropColumn(
                name: "ConflictPolicy",
                table: "BasketDraftPolicy");

            migrationBuilder.DropColumn(
                name: "MinimumAgreement",
                table: "BasketDraftPolicy");

            migrationBuilder.DropColumn(
                name: "MinimumStrategyConfidence",
                table: "BasketDraftPolicy");
        }
    }
}
