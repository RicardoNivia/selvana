using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.AI.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ia");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usos_modelo",
                schema: "ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agente = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nivel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    intento = table.Column<int>(type: "integer", nullable: false),
                    exito = table.Column<bool>(type: "boolean", nullable: false),
                    falla = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tokens_entrada = table.Column<long>(type: "bigint", nullable: false),
                    tokens_salida = table.Column<long>(type: "bigint", nullable: false),
                    costo_usd = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    duracion_ms = table.Column<long>(type: "bigint", nullable: false),
                    ocurrido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usos_modelo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "ia",
                table: "outbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_usos_modelo_empresa_id_ocurrido_en",
                schema: "ia",
                table: "usos_modelo",
                columns: new[] { "empresa_id", "ocurrido_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "ia");

            migrationBuilder.DropTable(
                name: "usos_modelo",
                schema: "ia");
        }
    }
}
