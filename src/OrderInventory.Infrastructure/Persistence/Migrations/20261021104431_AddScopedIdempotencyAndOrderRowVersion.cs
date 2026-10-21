using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderInventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedIdempotencyAndOrderRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "IdempotencyRecords",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "IdempotencyRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "IdempotencyRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CustomerId_Key",
                table: "IdempotencyRecords",
                columns: new[] { "CustomerId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_CustomerId_Key",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "IdempotencyRecords");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords",
                column: "Key");
        }
    }
}
