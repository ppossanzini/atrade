using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class ProposalStrategyAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LlmConfidence",
                table: "Proposal",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LlmRationale",
                table: "Proposal",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedScenario",
                table: "Proposal",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StrategyAgreement",
                table: "Proposal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmConfidence",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "LlmRationale",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "SelectedScenario",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "StrategyAgreement",
                table: "Proposal");
        }
    }
}
