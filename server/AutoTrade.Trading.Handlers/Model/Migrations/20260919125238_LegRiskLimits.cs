using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class LegRiskLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaxSpreadPips",
                table: "ProposalLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxVolatilityPercent",
                table: "ProposalLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxSpreadPips",
                table: "BasketVersionLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxVolatilityPercent",
                table: "BasketVersionLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxSpreadPips",
                table: "BasketDraftLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "MaxVolatilityPercent",
                table: "BasketDraftLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            // The limits used to be supplied from configuration, one pair per market. They are carried onto
            // the legs that already exist so that the same market is judged by the same limits before and
            // after this migration. Fx = 1.5 pips / 0.35 %, Metal = 40 / 0.8, Index = 5 / 0.6.
            migrationBuilder.Sql("UPDATE BasketDraftLeg SET MaxSpreadPips = 1.5, MaxVolatilityPercent = 0.35 WHERE Market = 0;");
            migrationBuilder.Sql("UPDATE BasketDraftLeg SET MaxSpreadPips = 40, MaxVolatilityPercent = 0.8 WHERE Market = 1;");
            migrationBuilder.Sql("UPDATE BasketDraftLeg SET MaxSpreadPips = 5, MaxVolatilityPercent = 0.6 WHERE Market = 2;");
            migrationBuilder.Sql("UPDATE BasketVersionLeg SET MaxSpreadPips = 1.5, MaxVolatilityPercent = 0.35 WHERE Market = 0;");
            migrationBuilder.Sql("UPDATE BasketVersionLeg SET MaxSpreadPips = 40, MaxVolatilityPercent = 0.8 WHERE Market = 1;");
            migrationBuilder.Sql("UPDATE BasketVersionLeg SET MaxSpreadPips = 5, MaxVolatilityPercent = 0.6 WHERE Market = 2;");
            migrationBuilder.Sql("UPDATE ProposalLeg SET MaxSpreadPips = 1.5, MaxVolatilityPercent = 0.35 WHERE Market = 0;");
            migrationBuilder.Sql("UPDATE ProposalLeg SET MaxSpreadPips = 40, MaxVolatilityPercent = 0.8 WHERE Market = 1;");
            migrationBuilder.Sql("UPDATE ProposalLeg SET MaxSpreadPips = 5, MaxVolatilityPercent = 0.6 WHERE Market = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxSpreadPips",
                table: "ProposalLeg");

            migrationBuilder.DropColumn(
                name: "MaxVolatilityPercent",
                table: "ProposalLeg");

            migrationBuilder.DropColumn(
                name: "MaxSpreadPips",
                table: "BasketVersionLeg");

            migrationBuilder.DropColumn(
                name: "MaxVolatilityPercent",
                table: "BasketVersionLeg");

            migrationBuilder.DropColumn(
                name: "MaxSpreadPips",
                table: "BasketDraftLeg");

            migrationBuilder.DropColumn(
                name: "MaxVolatilityPercent",
                table: "BasketDraftLeg");
        }
    }
}
