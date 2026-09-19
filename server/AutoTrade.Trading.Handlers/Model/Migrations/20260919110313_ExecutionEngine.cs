using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LegId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BrokerEventId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FailurePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumCoverage = table.Column<int>(type: "INTEGER", nullable: false),
                    Coverage = table.Column<int>(type: "INTEGER", nullable: false),
                    CompensationOfExecutionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Execution", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionLeg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumeUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientOrderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    BrokerOrderId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    FilledVolumeUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePrice = table.Column<double>(type: "REAL", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LastEventAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLeg", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEvent_BrokerEventId",
                table: "BrokerEvent",
                column: "BrokerEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrokerEvent_ExecutionId",
                table: "BrokerEvent",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Execution_ProposalId",
                table: "Execution",
                column: "ProposalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Execution_Status_CreatedAtUtc",
                table: "Execution",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLeg_ClientOrderId",
                table: "ExecutionLeg",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLeg_ExecutionId_Ordinal",
                table: "ExecutionLeg",
                columns: new[] { "ExecutionId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerEvent");

            migrationBuilder.DropTable(
                name: "Execution");

            migrationBuilder.DropTable(
                name: "ExecutionLeg");
        }
    }
}
