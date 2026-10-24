using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Vinculacion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vinculacion");

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "vinculacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "vinculacion",
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
                name: "procesos",
                schema: "vinculacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empresa_usuaria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    etapa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aprobada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    condiciones = table.Column<string>(type: "jsonb", nullable: false),
                    oferta = table.Column<string>(type: "jsonb", nullable: false),
                    persona = table.Column<string>(type: "jsonb", nullable: false),
                    vacante = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procesos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "vinculacion",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "vinculacion",
                table: "outbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_procesos_codigo",
                schema: "vinculacion",
                table: "procesos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procesos_empresa_id_actualizado_en",
                schema: "vinculacion",
                table: "procesos",
                columns: new[] { "empresa_id", "actualizado_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "vinculacion");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "vinculacion");

            migrationBuilder.DropTable(
                name: "procesos",
                schema: "vinculacion");
        }
    }
}
