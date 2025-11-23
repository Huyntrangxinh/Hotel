using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueConstraintAndAddOptionNameToRoomPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices");

            migrationBuilder.AddColumn<string>(
                name: "OptionName",
                table: "RoomPrices",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

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
                name: "IX_RoomPrices_PropertyId",
                table: "RoomPrices",
                column: "PropertyId");

            migrationBuilder.AddForeignKey(
                name: "FK_RoomPrices_PricePackages_PricePackageId",
                table: "RoomPrices",
                column: "PricePackageId",
                principalTable: "PricePackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.DropIndex(
                name: "IX_RoomPrices_PropertyId",
                table: "RoomPrices");

            migrationBuilder.DropColumn(
                name: "OptionName",
                table: "RoomPrices");

            migrationBuilder.DropColumn(
                name: "PricePackageId",
                table: "RoomPrices");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPrices_PropertyId_RoomId",
                table: "RoomPrices",
                columns: new[] { "PropertyId", "RoomId" },
                unique: true);
        }
    }
}
