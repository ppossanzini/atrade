using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class StrategyEntryMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The declared entry rule arrives with a default instead of a backfill: 0 is RegimeMomentum,
            // which is the default the operator confirmed in the prototype, so a policy that existed before
            // this column was introduced adopts the same declared rule the prototype was showing.
            migrationBuilder.AddColumn<int>(
                name: "EntryMode",
                table: "Proposal",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntryMode",
                table: "BasketVersionPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EntryMode",
                table: "BasketDraftPolicy",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryMode",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "EntryMode",
                table: "BasketVersionPolicy");

            migrationBuilder.DropColumn(
                name: "EntryMode",
                table: "BasketDraftPolicy");
        }
    }
}
