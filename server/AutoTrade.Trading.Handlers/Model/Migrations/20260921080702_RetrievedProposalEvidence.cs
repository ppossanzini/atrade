using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class RetrievedProposalEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProposalEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "TEXT", nullable: true),
                    QueryHash = table.Column<string>(type: "TEXT", nullable: true),
                    SourceRef = table.Column<string>(type: "TEXT", nullable: true),
                    RetrievedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalEvidence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalEvidence_ProposalId_Rank",
                table: "ProposalEvidence",
                columns: new[] { "ProposalId", "Rank" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProposalEvidence");
        }
    }
}
