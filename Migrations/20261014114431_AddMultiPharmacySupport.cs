using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiPharmacySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Locations_Code",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.CreateTable(
                name: "Pharmacies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pharmacies", x => x.Id);
                });

            // Seed default primary pharmacy for historical data
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM Pharmacies) BEGIN SET IDENTITY_INSERT Pharmacies ON; INSERT INTO Pharmacies (Id, Name, IsActive, CreatedAt) VALUES (1, 'Main Pharmacy', 1, GETUTCDATE()); SET IDENTITY_INSERT Pharmacies OFF; END");

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Sales",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Purchases",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Locations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PharmacyId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Backfill existing users to pharmacy 1 if null
            migrationBuilder.Sql("UPDATE Users SET PharmacyId = 1 WHERE PharmacyId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PharmacyId",
                table: "Users",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PharmacyId",
                table: "Suppliers",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PharmacyId",
                table: "Sales",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PharmacyId_InvoiceNumber",
                table: "Sales",
                columns: new[] { "PharmacyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PharmacyId",
                table: "Purchases",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_PharmacyId_InvoiceNumber",
                table: "Purchases",
                columns: new[] { "PharmacyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_PharmacyId",
                table: "Products",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_PharmacyId_Code",
                table: "Locations",
                columns: new[] { "PharmacyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PharmacyId",
                table: "Expenses",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PharmacyId",
                table: "Customers",
                column: "PharmacyId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_PharmacyId_Name",
                table: "Categories",
                columns: new[] { "PharmacyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PharmacyId",
                table: "AuditLogs",
                column: "PharmacyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Pharmacies_PharmacyId",
                table: "AuditLogs",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Pharmacies_PharmacyId",
                table: "Categories",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Pharmacies_PharmacyId",
                table: "Customers",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Pharmacies_PharmacyId",
                table: "Expenses",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_Pharmacies_PharmacyId",
                table: "Locations",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Pharmacies_PharmacyId",
                table: "Products",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Pharmacies_PharmacyId",
                table: "Purchases",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Pharmacies_PharmacyId",
                table: "Sales",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Pharmacies_PharmacyId",
                table: "Suppliers",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Pharmacies_PharmacyId",
                table: "Users",
                column: "PharmacyId",
                principalTable: "Pharmacies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Pharmacies_PharmacyId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Pharmacies_PharmacyId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Pharmacies_PharmacyId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Pharmacies_PharmacyId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_Pharmacies_PharmacyId",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Pharmacies_PharmacyId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Pharmacies_PharmacyId",
                table: "Purchases");

            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Pharmacies_PharmacyId",
                table: "Sales");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Pharmacies_PharmacyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Pharmacies_PharmacyId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Pharmacies");

            migrationBuilder.DropIndex(
                name: "IX_Users_PharmacyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PharmacyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PharmacyId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PharmacyId_InvoiceNumber",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PharmacyId",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_PharmacyId_InvoiceNumber",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Products_PharmacyId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Locations_PharmacyId_Code",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_PharmacyId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PharmacyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Categories_PharmacyId_Name",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_PharmacyId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PharmacyId",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_InvoiceNumber",
                table: "Sales",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Code",
                table: "Locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
