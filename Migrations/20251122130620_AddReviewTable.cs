using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPrices_PricePackages_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId_RoomId_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.DropColumn(
                name: "PricePackageId",
                table: "RoomPrices");

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BookingId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CleanlinessRating = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceRating = table.Column<int>(type: "INTEGER", nullable: true),
                    ValueRating = table.Column<int>(type: "INTEGER", nullable: true),
                    LocationRating = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices",
                columns: new[] { "PropertyId", "RoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingId",
                table: "Reviews",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PropertyId",
                table: "Reviews",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices");

            migrationBuilder.AddColumn<int>(
                name: "PricePackageId",
                table: "RoomPrices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PricePackageId",
                table: "RoomPrices",
                column: "PricePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PropertyId_RoomId_PricePackageId",
                table: "RoomPrices",
                columns: new[] { "PropertyId", "RoomId", "PricePackageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPrices_PricePackages_PricePackageId",
                table: "RoomPrices",
                column: "PricePackageId",
                principalTable: "PricePackages",
                principalColumn: "Id");
        }
    }
}
