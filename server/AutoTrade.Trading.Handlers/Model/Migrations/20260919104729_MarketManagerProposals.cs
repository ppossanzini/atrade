using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class MarketManagerProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastCycleAtUtc",
                table: "MarketManagerState",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GateEvaluation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    Verdict = table.Column<int>(type: "INTEGER", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Market = table.Column<int>(type: "INTEGER", nullable: true),
                    ObservedValue = table.Column<double>(type: "REAL", nullable: true),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GateEvaluation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketSnapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccountEquity = table.Column<double>(type: "REAL", nullable: false),
                    AccountBalance = table.Column<double>(type: "REAL", nullable: false),
                    RealizedPnlToday = table.Column<double>(type: "REAL", nullable: false),
                    UnrealizedPnl = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketSnapshotLeg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<double>(type: "REAL", nullable: true),
                    SpreadPips = table.Column<double>(type: "REAL", nullable: true),
                    VolatilityPercent = table.Column<double>(type: "REAL", nullable: true),
                    IsTradable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketSnapshotLeg", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proposal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    Gate = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedRiskPercent = table.Column<double>(type: "REAL", nullable: false),
                    ProposedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DecidedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecisionReason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Rationale = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CycleSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProposalLeg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskCap = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalLeg", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GateEvaluation_ProposalId_Ordinal",
                table: "GateEvaluation",
                columns: new[] { "ProposalId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshotLeg_SnapshotId_Ordinal",
                table: "MarketSnapshotLeg",
                columns: new[] { "SnapshotId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposal_BasketId",
                table: "Proposal",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposal_SnapshotId",
                table: "Proposal",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposal_Status_ProposedAtUtc",
                table: "Proposal",
                columns: new[] { "Status", "ProposedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalLeg_ProposalId_Ordinal",
                table: "ProposalLeg",
                columns: new[] { "ProposalId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GateEvaluation");

            migrationBuilder.DropTable(
                name: "MarketSnapshot");

            migrationBuilder.DropTable(
                name: "MarketSnapshotLeg");

            migrationBuilder.DropTable(
                name: "Proposal");

            migrationBuilder.DropTable(
                name: "ProposalLeg");

            migrationBuilder.DropColumn(
                name: "LastCycleAtUtc",
                table: "MarketManagerState");
        }
    }
}
