using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTrade.Trading.Handlers.Model.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveBasketVersion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActivatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveBasketVersion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Basket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Basket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketDraftLeg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskCap = table.Column<double>(type: "REAL", nullable: false),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDraftLeg", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketDraftPolicy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FailurePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumCoverage = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskPerBasket = table.Column<double>(type: "REAL", nullable: false),
                    DailyLossLimit = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketDraftPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BasketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketVersion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketVersionLeg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Market = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskCap = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketVersionLeg", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BasketVersionPolicy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FailurePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumCoverage = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskPerBasket = table.Column<double>(type: "REAL", nullable: false),
                    DailyLossLimit = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasketVersionPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorType = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KillSwitchState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IsEngaged = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangedByOperatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KillSwitchState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketManagerState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAnalysisRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketManagerState", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operator", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperatorSession",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionToken = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedReason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorSession", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingAccount",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrokerAccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    Environment = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTradingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConnectionState = table.Column<int>(type: "INTEGER", nullable: false),
                    LastBrokerSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastReconciledUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingAccount", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Basket_Name",
                table: "Basket",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_BasketDraftLeg_BasketId",
                table: "BasketDraftLeg",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketDraftPolicy_BasketId",
                table: "BasketDraftPolicy",
                column: "BasketId");

            migrationBuilder.CreateIndex(
                name: "IX_BasketVersion_BasketId_Number",
                table: "BasketVersion",
                columns: new[] { "BasketId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketVersionLeg_VersionId_Ordinal",
                table: "BasketVersionLeg",
                columns: new[] { "VersionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketVersionLeg_VersionId_Symbol",
                table: "BasketVersionLeg",
                columns: new[] { "VersionId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasketVersionPolicy_VersionId",
                table: "BasketVersionPolicy",
                column: "VersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEvent_OccurredAtUtc",
                table: "JournalEvent",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEvent_Sequence",
                table: "JournalEvent",
                column: "Sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Operator_UserName",
                table: "Operator",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorSession_SessionToken",
                table: "OperatorSession",
                column: "SessionToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveBasketVersion");

            migrationBuilder.DropTable(
                name: "Basket");

            migrationBuilder.DropTable(
                name: "BasketDraftLeg");

            migrationBuilder.DropTable(
                name: "BasketDraftPolicy");

            migrationBuilder.DropTable(
                name: "BasketVersion");

            migrationBuilder.DropTable(
                name: "BasketVersionLeg");

            migrationBuilder.DropTable(
                name: "BasketVersionPolicy");

            migrationBuilder.DropTable(
                name: "JournalEvent");

            migrationBuilder.DropTable(
                name: "KillSwitchState");

            migrationBuilder.DropTable(
                name: "MarketManagerState");

            migrationBuilder.DropTable(
                name: "Operator");

            migrationBuilder.DropTable(
                name: "OperatorSession");

            migrationBuilder.DropTable(
                name: "TradingAccount");
        }
    }
}
