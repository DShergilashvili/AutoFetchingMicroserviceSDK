using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresidentsMap.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gifts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    To = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    From = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    BuyerEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    RecipientEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    Delivered = table.Column<bool>(type: "INTEGER", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    Lang = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    Coups = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPaid = table.Column<int>(type: "INTEGER", nullable: false),
                    Immune = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaymentRef = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CabinetPrepaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    CabinetPaid = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GiftId = table.Column<string>(type: "TEXT", nullable: false),
                    Post = table.Column<string>(type: "TEXT", nullable: false),
                    CustomTitle = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Price = table.Column<int>(type: "INTEGER", nullable: false),
                    Worth = table.Column<int>(type: "INTEGER", nullable: false),
                    Coups = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPaid = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentRef = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Gifts_GiftId",
                        column: x => x.GiftId,
                        principalTable: "Gifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_GiftId_Post",
                table: "Appointments",
                columns: new[] { "GiftId", "Post" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_Country_Date",
                table: "Gifts",
                columns: new[] { "Country", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_CreatedAt",
                table: "Gifts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_Delivered_Date",
                table: "Gifts",
                columns: new[] { "Delivered", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Gifts");
        }
    }
}
