using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddPricePackageIdToRoomPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PricePackageId",
                table: "RoomPrices",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PricePackageId",
                table: "RoomPrices",
                column: "PricePackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPrices_PricePackages_PricePackageId",
                table: "RoomPrices",
                column: "PricePackageId",
                principalTable: "PricePackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RoomPrices_PricePackages_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PricePackageId",
                table: "RoomPrices");

            migrationBuilder.DropColumn(
                name: "PricePackageId",
                table: "RoomPrices");
        }
    }
}
