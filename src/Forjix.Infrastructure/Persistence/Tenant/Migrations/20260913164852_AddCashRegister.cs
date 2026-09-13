using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forjix.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashRegisters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpeningAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosingAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashSessions_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalTable: "CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_CashSessions_CashSessionId",
                        column: x => x.CashSessionId,
                        principalTable: "CashSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashSessionId_CreatedAt",
                table: "CashMovements",
                columns: new[] { "CashSessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_CashRegisterId",
                table: "CashSessions",
                column: "CashRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_OpenedAt",
                table: "CashSessions",
                column: "OpenedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "CashSessions");

            migrationBuilder.DropTable(
                name: "CashRegisters");
        }
    }
}
