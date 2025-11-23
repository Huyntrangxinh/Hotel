using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintOnRoomPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PropertyId_RoomId_PricePackageId",
                table: "RoomPrices",
                columns: new[] { "PropertyId", "RoomId", "PricePackageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId_RoomId_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices",
                columns: new[] { "PropertyId", "RoomId" },
                unique: true);
        }
    }
}
