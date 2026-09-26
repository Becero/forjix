using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Forjix.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedInventoryV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.adjust')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000001',N'stock.adjust',N'Ajustar estoque',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.adjust'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.inventory.view')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000002',N'stock.inventory.view',N'Visualizar inventários',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.inventory.view'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.inventory.create')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000003',N'stock.inventory.create',N'Criar e editar inventários',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.inventory.create'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.inventory.count')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000004',N'stock.inventory.count',N'Iniciar e contar inventários',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.inventory.count'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.inventory.complete')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000005',N'stock.inventory.complete',N'Finalizar inventários',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.inventory.complete'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.inventory.cancel')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000006',N'stock.inventory.cancel',N'Cancelar inventários',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.inventory.cancel'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Code = 'stock.reports')
INSERT INTO Permissions (Id,Code,Name,Module) VALUES ('8b000000-0000-0000-0000-000000000007',N'stock.reports',N'Consultar relatórios de estoque',N'Estoque');
INSERT INTO RolePermissions (RoleId,PermissionId,GrantedAt)
SELECT r.Id,p.Id,SYSUTCDATETIME() FROM Roles r CROSS JOIN Permissions p
WHERE r.IsSystem=1 AND r.NormalizedName=N'ADMINISTRADOR' AND p.Code=N'stock.reports'
AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.RoleId=r.Id AND rp.PermissionId=p.Id);
""");
            migrationBuilder.AddColumn<string>(
                name: "Observation",
                table: "InventoryMovements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "InventoryMovements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stocktakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocktakes", x => x.Id);
                    table.CheckConstraint("CK_Stocktake_Closed", "([Status] IN (0,1) AND [ClosedAt] IS NULL) OR ([Status] IN (2,3) AND [ClosedAt] IS NOT NULL)");
                    table.CheckConstraint("CK_Stocktake_Status", "[Status] IN (0,1,2,3)");
                    table.ForeignKey(
                        name: "FK_Stocktakes_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stocktakes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeSequences", x => x.Id);
                    table.CheckConstraint("CK_StocktakeSequence", "[Id] = 1 AND [LastValue] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "StocktakeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StocktakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CountedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CountedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StockRowVersion = table.Column<byte[]>(type: "varbinary(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocktakeItems", x => x.Id);
                    table.CheckConstraint("CK_StocktakeItem_Count", "[CountedQuantity] IS NULL OR ([CountedQuantity] >= 0 AND [CountedAt] IS NOT NULL AND [CountedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StocktakeItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeItems_Stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalTable: "Stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StocktakeItems_Users_CountedByUserId",
                        column: x => x.CountedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_CountedByUserId",
                table: "StocktakeItems",
                column: "CountedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_ProductId",
                table: "StocktakeItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeItems_StocktakeId_ProductId",
                table: "StocktakeItems",
                columns: new[] { "StocktakeId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_Number",
                table: "Stocktakes",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_OpenedAt",
                table: "Stocktakes",
                column: "OpenedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_Status_OpenedAt",
                table: "Stocktakes",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_UpdatedByUserId",
                table: "Stocktakes",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_UserId",
                table: "Stocktakes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DELETE rp FROM RolePermissions rp JOIN Permissions p ON p.Id=rp.PermissionId WHERE p.Code IN ('stock.adjust','stock.inventory.view','stock.inventory.create','stock.inventory.count','stock.inventory.complete','stock.inventory.cancel','stock.reports');
DELETE FROM Permissions WHERE Code IN ('stock.adjust','stock.inventory.view','stock.inventory.create','stock.inventory.count','stock.inventory.complete','stock.inventory.cancel','stock.reports');
""");
            migrationBuilder.DropTable(
                name: "StocktakeItems");

            migrationBuilder.DropTable(
                name: "StocktakeSequences");

            migrationBuilder.DropTable(
                name: "Stocktakes");

            migrationBuilder.DropColumn(
                name: "Observation",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "InventoryMovements");
        }
    }
}
