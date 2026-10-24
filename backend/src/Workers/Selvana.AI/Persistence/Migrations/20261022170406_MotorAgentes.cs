using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.AI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MotorAgentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acciones_agentes",
                schema: "ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecucion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    caso = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    paso = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    costo_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    duracion_ms = table.Column<long>(type: "bigint", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acciones_agentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memorias_caso",
                schema: "ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    caso = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    hechos = table.Column<string>(type: "jsonb", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memorias_caso", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_acciones_agentes_ejecucion_id",
                schema: "ia",
                table: "acciones_agentes",
                column: "ejecucion_id");

            migrationBuilder.CreateIndex(
                name: "ix_acciones_agentes_empresa_id_caso_ocurrido_en",
                schema: "ia",
                table: "acciones_agentes",
                columns: new[] { "empresa_id", "caso", "ocurrido_en" });

            migrationBuilder.CreateIndex(
                name: "ix_memorias_caso_empresa_id_agente_caso",
                schema: "ia",
                table: "memorias_caso",
                columns: new[] { "empresa_id", "agente", "caso" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acciones_agentes",
                schema: "ia");

            migrationBuilder.DropTable(
                name: "memorias_caso",
                schema: "ia");
        }
    }
}
