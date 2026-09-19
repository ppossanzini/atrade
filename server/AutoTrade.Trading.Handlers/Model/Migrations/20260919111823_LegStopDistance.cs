using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class LegStopDistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "StopDistancePips",
                table: "BasketVersionLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "StopDistancePips",
                table: "BasketDraftLeg",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StopDistancePips",
                table: "BasketVersionLeg");

            migrationBuilder.DropColumn(
                name: "StopDistancePips",
                table: "BasketDraftLeg");
        }
    }
}
