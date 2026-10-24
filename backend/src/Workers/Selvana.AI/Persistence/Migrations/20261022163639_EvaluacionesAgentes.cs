using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.AI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvaluacionesAgentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluaciones_agentes",
                schema: "ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    casos = table.Column<int>(type: "integer", nullable: false),
                    fallidos = table.Column<int>(type: "integer", nullable: false),
                    metricas = table.Column<string>(type: "jsonb", nullable: false),
                    metas = table.Column<string>(type: "jsonb", nullable: false),
                    cumple_metas = table.Column<bool>(type: "boolean", nullable: false),
                    costo_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    modelos = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    errores = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluaciones_agentes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evaluaciones_agentes_agente_fecha",
                schema: "ia",
                table: "evaluaciones_agentes",
                columns: new[] { "agente", "fecha" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluaciones_agentes",
                schema: "ia");
        }
    }
}
