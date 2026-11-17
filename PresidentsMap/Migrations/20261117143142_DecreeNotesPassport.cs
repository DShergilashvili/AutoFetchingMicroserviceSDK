using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresidentsMap.Migrations
{
    /// <inheritdoc />
    public partial class DecreeNotesPassport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Decree",
                table: "Gifts",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GiftId = table.Column<string>(type: "TEXT", nullable: false),
                    From = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentRef = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GiftId = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    To = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    BuyerEmail = table.Column<string>(type: "TEXT", nullable: false),
                    RecipientEmail = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_GiftId",
                table: "Notes",
                column: "GiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Reigns_BuyerEmail",
                table: "Reigns",
                column: "BuyerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Reigns_GiftId",
                table: "Reigns",
                column: "GiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Reigns_RecipientEmail",
                table: "Reigns",
                column: "RecipientEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Reigns");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropColumn(
                name: "Decree",
                table: "Gifts");
        }
    }
}
